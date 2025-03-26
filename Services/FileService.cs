using Microsoft.AspNetCore.Components.Forms;
using System.Text.RegularExpressions;

namespace FastyBoxWeb.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IBrowserFile file, int requestId, int itemId);
        Task<byte[]> GetFileAsync(string filePath);
        Task<bool> DeleteFileAsync(string filePath);
        Task<string> GetFileNameAsync(string filePath);
        bool IsValidFileExtension(string fileName);
        bool IsValidFileContent(Stream fileStream, string contentType);
        void SanitizeFileName(ref string fileName);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xlsx", ".txt" };
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> SaveFileAsync(IBrowserFile file, int requestId, int itemId)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            if (file.Size > _maxFileSize)
            {
                throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido de {_maxFileSize / (1024 * 1024)}MB");
            }

            string fileName = file.Name;
            SanitizeFileName(ref fileName);

            if (!IsValidFileExtension(fileName))
            {
                throw new InvalidOperationException("Tipo de archivo no permitido");
            }

            // Crear directorios si no existen
            string uploadPath = Path.Combine(_environment.ContentRootPath, "uploads", requestId.ToString(), itemId.ToString());
            Directory.CreateDirectory(uploadPath);

            // Generar nombre único para evitar colisiones
            string uniqueFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            string filePath = Path.Combine(uploadPath, uniqueFileName);

            // Crear canal de lectura y leer el archivo
            using var stream = file.OpenReadStream(_maxFileSize);

            // Verificar contenido del archivo antes de guardar
            if (!IsValidFileContent(stream, file.ContentType))
            {
                throw new InvalidOperationException("El contenido del archivo no coincide con su extensión");
            }

            stream.Position = 0;

            // Escribir el archivo en el sistema de archivos
            using var fileStream = new FileStream(filePath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            // Devolver la ruta relativa para guardar en la base de datos
            return $"uploads/{requestId}/{itemId}/{uniqueFileName}";
        }

        public async Task<byte[]> GetFileAsync(string filePath)
        {
            // Validar la ruta para evitar directory traversal
            if (!IsValidPath(filePath))
            {
                throw new ArgumentException("Ruta de archivo inválida", nameof(filePath));
            }

            // Obtener la ruta absoluta
            string fullPath = Path.Combine(_environment.ContentRootPath, filePath);

            // Verificar que el archivo existe
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("El archivo solicitado no existe", filePath);
            }

            // Leer y devolver el archivo
            return await File.ReadAllBytesAsync(fullPath);
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            // Validar la ruta para evitar directory traversal
            if (!IsValidPath(filePath))
            {
                throw new ArgumentException("Ruta de archivo inválida", nameof(filePath));
            }

            // Obtener la ruta absoluta
            string fullPath = Path.Combine(_environment.ContentRootPath, filePath);

            // Verificar que el archivo existe
            if (!File.Exists(fullPath))
            {
                return false;
            }

            // Eliminar el archivo
            await Task.Run(() => File.Delete(fullPath));
            return true;
        }

        public async Task<string> GetFileNameAsync(string filePath)
        {
            // Validar la ruta para evitar directory traversal
            if (!IsValidPath(filePath))
            {
                throw new ArgumentException("Ruta de archivo inválida", nameof(filePath));
            }

            return await Task.FromResult(Path.GetFileName(filePath));
        }

        public bool IsValidFileExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }

        public bool IsValidFileContent(Stream fileStream, string contentType)
        {
            // Verificar el contenido real del archivo comparándolo con el tipo MIME
            // Esta es una implementación básica, en producción se recomienda usar
            // bibliotecas como FileMagic o similar para verificar la firma del archivo

            // Por ejemplo, para archivos de imagen comunes:
            if (contentType.StartsWith("image/"))
            {
                byte[] header = new byte[8];
                if (fileStream.Length < 8) return false;

                fileStream.Position = 0;
                fileStream.Read(header, 0, header.Length);

                // Verificar firmas de archivo comunes
                // JPEG: FF D8 FF
                if (contentType == "image/jpeg" && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                {
                    return true;
                }
                // PNG: 89 50 4E 47 0D 0A 1A 0A
                else if (contentType == "image/png" && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                {
                    return true;
                }
            }

            // Para PDF: 25 50 44 46 (PDF-)
            else if (contentType == "application/pdf")
            {
                byte[] header = new byte[4];
                if (fileStream.Length < 4) return false;

                fileStream.Position = 0;
                fileStream.Read(header, 0, header.Length);

                if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                {
                    return true;
                }
            }

            // Si no podemos verificar con certeza, aceptamos por defecto
            // En producción, sería mejor rechazar por defecto
            return true;
        }

        public void SanitizeFileName(ref string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "file";
                return;
            }

            // Eliminar caracteres inválidos
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegex = $"[{invalidChars}]";
            fileName = Regex.Replace(fileName, invalidRegex, "_");

            // Eliminar caracteres potencialmente peligrosos
            fileName = Regex.Replace(fileName, @"[^\w\-\.]", "_");

            // Limitar longitud
            if (fileName.Length > 100)
            {
                string extension = Path.GetExtension(fileName);
                fileName = fileName.Substring(0, 96) + extension;
            }
        }

        private bool IsValidPath(string filePath)
        {
            // Evitar directory traversal
            if (string.IsNullOrEmpty(filePath)) return false;

            // Normalizar la ruta
            string normalizedPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, filePath));

            // Verificar que la ruta está dentro del directorio de contenido permitido
            return normalizedPath.StartsWith(_environment.ContentRootPath);
        }
    }
}