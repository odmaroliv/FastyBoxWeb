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
        Task<string> SaveFileDataAsync(byte[] fileData, string fileName, string contentType, int requestId, int itemId);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private readonly string[] _allowedExtensions = { ".jpg", ".heic", ".jpeg", ".png", ".pdf", ".webp", ".bmp" };
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB
        private readonly string[] _allowedFileTypes = {
            "image/jpeg",    // Para JPG y JPEG
            "image/png",     // Para PNG
            "application/pdf", // Para PDF
            "image/heic",    // Para HEIC
            "image/heif",    // Variante de HEIC
            "image/webp",    // Para WEBP
            "image/bmp"      // Para BMP
        };


        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        // Reemplaza el método SaveFileAsync en tu FileService.cs con este:

        public async Task<string> SaveFileAsync(IBrowserFile file, int requestId, int itemId)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            if (file.Size > _maxFileSize)
            {
                throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido de {_maxFileSize / (1024 * 1024)}MB");
            }

            // Verificar tipo MIME
            if (!_allowedFileTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException($"Tipo de archivo '{file.ContentType}' no permitido");
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

            // Generar nombre único para evitar colisiones y ofuscación adicional
            string uniqueFileName = $"{Path.GetRandomFileName()}_{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            string filePath = Path.Combine(uploadPath, uniqueFileName);

            // Abrir stream para lectura
            using var stream = file.OpenReadStream(_maxFileSize);

            // Leer los primeros bytes para verificación sin modificar la posición
            byte[] header = new byte[16]; // Suficiente para todas las verificaciones
            int bytesRead = await stream.ReadAsync(header, 0, Math.Min(header.Length, (int)file.Size));

            // Verificar el contenido basado en los bytes del encabezado
            if (!IsValidFileHeader(header, bytesRead, file.ContentType))
            {
                throw new InvalidOperationException("El contenido del archivo no coincide con su extensión");
            }

            // Como ya consumimos parte del stream, necesitamos crear uno nuevo
            using var newStream = file.OpenReadStream(_maxFileSize);

            // Escribir el archivo en el sistema de archivos
            using var fileStream = new FileStream(filePath, FileMode.Create);
            await newStream.CopyToAsync(fileStream);

            // Devolver la ruta relativa para guardar en la base de datos
            return $"uploads/{requestId}/{itemId}/{uniqueFileName}";
        }

        // Añade este nuevo método en FileService.cs:

        /// <summary>
        /// Verifica si el encabezado del archivo corresponde al tipo MIME indicado.
        /// Este método no modifica la posición del stream.
        /// </summary>
        private bool IsValidFileHeader(byte[] header, int bytesRead, string contentType)
        {
            try
            {
                // Normalizar contentType a minúsculas para comparación
                string normalizedContentType = contentType.ToLowerInvariant().Trim();

                // Verificar según el tipo de contenido
                bool isValid = false;

                // JPEG - verifica FF D8 FF (primeros 3 bytes de cualquier JPEG)
                if (normalizedContentType.Contains("jpeg") || normalizedContentType.Contains("jpg"))
                {
                    isValid = bytesRead >= 3 &&
                              header[0] == 0xFF &&
                              header[1] == 0xD8 &&
                              header[2] == 0xFF;
                }
                // PNG - verifica la firma completa de 8 bytes
                else if (normalizedContentType.Contains("png"))
                {
                    isValid = bytesRead >= 8 &&
                              header[0] == 0x89 &&
                              header[1] == 0x50 && // P
                              header[2] == 0x4E && // N
                              header[3] == 0x47 && // G
                              header[4] == 0x0D && // CR
                              header[5] == 0x0A && // LF
                              header[6] == 0x1A && // EOF
                              header[7] == 0x0A;   // LF
                }
                // GIF - verifica "GIF87a" o "GIF89a"
                else if (normalizedContentType.Contains("gif"))
                {
                    isValid = bytesRead >= 6 &&
                              header[0] == 0x47 && // G
                              header[1] == 0x49 && // I
                              header[2] == 0x46 && // F
                              header[3] == 0x38 && // 8
                              (
                                  (header[4] == 0x37 && header[5] == 0x61) || // 7a (GIF87a)
                                  (header[4] == 0x39 && header[5] == 0x61)    // 9a (GIF89a)
                              );
                }
                // BMP - verifica "BM" al inicio
                else if (normalizedContentType.Contains("bmp"))
                {
                    isValid = bytesRead >= 2 &&
                              header[0] == 0x42 && // B
                              header[1] == 0x4D;   // M
                }
                // WEBP - verifica "RIFF" y luego "WEBP"
                else if (normalizedContentType.Contains("webp"))
                {
                    isValid = bytesRead >= 12 &&
                              header[0] == 0x52 && // R
                              header[1] == 0x49 && // I
                              header[2] == 0x46 && // F
                              header[3] == 0x46 && // F
                                                   // Los siguientes 4 bytes son el tamaño, que puede variar
                              header[8] == 0x57 && // W
                              header[9] == 0x45 && // E
                              header[10] == 0x42 && // B
                              header[11] == 0x50;  // P
                }
                // HEIC/HEIF - estructura más compleja, verificamos "ftyp" seguido de identificadores comunes
                else if (normalizedContentType.Contains("heic") || normalizedContentType.Contains("heif"))
                {
                    // Verificación simplificada para HEIC/HEIF
                    if (bytesRead >= 12)
                    {
                        // Buscar "ftyp" que generalmente está en la posición 4
                        for (int i = 0; i < bytesRead - 8; i++)
                        {
                            if (header[i] == 0x66 && // f
                                header[i + 1] == 0x74 && // t
                                header[i + 2] == 0x79 && // y
                                header[i + 3] == 0x70) // p
                            {
                                // Luego verificamos si hay identificadores comunes de HEIC/HEIF
                                if (i + 8 < bytesRead &&
                                    ((header[i + 4] == 0x68 && header[i + 5] == 0x65 && header[i + 6] == 0x69 && header[i + 7] == 0x63) || // "heic"
                                     (header[i + 4] == 0x68 && header[i + 5] == 0x65 && header[i + 6] == 0x69 && header[i + 7] == 0x78) || // "heix"
                                     (header[i + 4] == 0x6D && header[i + 5] == 0x69 && header[i + 6] == 0x66 && header[i + 7] == 0x31))) // "mif1"
                                {
                                    isValid = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                // PDF - verifica "%PDF-"
                else if (normalizedContentType.Contains("pdf"))
                {
                    isValid = bytesRead >= 5 &&
                              header[0] == 0x25 && // %
                              header[1] == 0x50 && // P
                              header[2] == 0x44 && // D
                              header[3] == 0x46 && // F
                              header[4] == 0x2D;   // -
                }
                // Para imágenes de dispositivos móviles que pueden tener formatos específicos
                // pero que se declaran como image/jpeg o image/png, damos algo de flexibilidad
                else if (normalizedContentType.StartsWith("image/"))
                {
                    // Intenta verificar si es alguno de los formatos de imagen conocidos
                    isValid = IsImageFileHeader(header, bytesRead);
                }

                _logger.LogInformation("Verificación de contenido para {ContentType}: {IsValid}", contentType, isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando contenido del archivo con tipo {ContentType}", contentType);
                return false; // Si hay un error, rechazamos el archivo por seguridad
            }
        }

        /// <summary>
        /// Método auxiliar que verifica si el encabezado corresponde a una imagen reconocida,
        /// independientemente del tipo MIME declarado
        /// </summary>
        private bool IsImageFileHeader(byte[] header, int bytesRead)
        {
            // JPEG
            if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG
            if (bytesRead >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return true;

            // GIF
            if (bytesRead >= 6 &&
                header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
                ((header[4] == 0x37 && header[5] == 0x61) || (header[4] == 0x39 && header[5] == 0x61)))
                return true;

            // BMP
            if (bytesRead >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                return true;

            // WEBP
            if (bytesRead >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            // HEIC/HEIF - verificación simplificada
            if (bytesRead >= 12)
            {
                for (int i = 0; i < bytesRead - 8; i++)
                {
                    if (header[i] == 0x66 && header[i + 1] == 0x74 && header[i + 2] == 0x79 && header[i + 3] == 0x70)
                    {
                        if (i + 8 < bytesRead &&
                            ((header[i + 4] == 0x68 && header[i + 5] == 0x65 && header[i + 6] == 0x69) || // "hei"
                             (header[i + 4] == 0x6D && header[i + 5] == 0x69 && header[i + 6] == 0x66))) // "mif"
                        {
                            return true;
                        }
                    }
                }
            }

            // No es ningún formato de imagen reconocido
            return false;
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

        /// <summary>
        /// Verifica que el contenido del archivo corresponda realmente al tipo MIME declarado
        /// mediante la comprobación de firmas de bytes (magic numbers)
        /// </summary>
        /// <param name="fileStream">Stream del archivo a verificar</param>
        /// <param name="contentType">Tipo MIME declarado</param>
        /// <returns>True si el contenido coincide con el tipo declarado, False en caso contrario</returns>
        public bool IsValidFileContent(Stream fileStream, string contentType)
        {
            if (fileStream == null || fileStream.Length < 4)
                return false;

            // Guardar la posición actual del stream para restaurarla después
            long originalPosition = fileStream.Position;

            try
            {
                // Retroceder al inicio del stream para leer los bytes de la cabecera
                fileStream.Position = 0;

                // Usamos un buffer lo suficientemente grande para todas las firmas
                byte[] header = new byte[16];
                int bytesRead = fileStream.Read(header, 0, Math.Min(header.Length, (int)fileStream.Length));

                // Normalizar contentType a minúsculas para comparación
                string normalizedContentType = contentType.ToLowerInvariant().Trim();

                // Verificar según el tipo de contenido
                bool isValid = false;

                // JPEG - verifica FF D8 FF (primeros 3 bytes de cualquier JPEG)
                if (normalizedContentType.Contains("jpeg") || normalizedContentType.Contains("jpg"))
                {
                    isValid = bytesRead >= 3 &&
                              header[0] == 0xFF &&
                              header[1] == 0xD8 &&
                              header[2] == 0xFF;
                }
                // PNG - verifica la firma completa de 8 bytes
                else if (normalizedContentType.Contains("png"))
                {
                    isValid = bytesRead >= 8 &&
                              header[0] == 0x89 &&
                              header[1] == 0x50 && // P
                              header[2] == 0x4E && // N
                              header[3] == 0x47 && // G
                              header[4] == 0x0D && // CR
                              header[5] == 0x0A && // LF
                              header[6] == 0x1A && // EOF
                              header[7] == 0x0A;   // LF
                }
                // GIF - verifica "GIF87a" o "GIF89a"
                else if (normalizedContentType.Contains("gif"))
                {
                    isValid = bytesRead >= 6 &&
                              header[0] == 0x47 && // G
                              header[1] == 0x49 && // I
                              header[2] == 0x46 && // F
                              header[3] == 0x38 && // 8
                              (
                                  (header[4] == 0x37 && header[5] == 0x61) || // 7a (GIF87a)
                                  (header[4] == 0x39 && header[5] == 0x61)    // 9a (GIF89a)
                              );
                }
                // BMP - verifica "BM" al inicio
                else if (normalizedContentType.Contains("bmp"))
                {
                    isValid = bytesRead >= 2 &&
                              header[0] == 0x42 && // B
                              header[1] == 0x4D;   // M
                }
                // WEBP - verifica "RIFF" y luego "WEBP"
                else if (normalizedContentType.Contains("webp"))
                {
                    isValid = bytesRead >= 12 &&
                              header[0] == 0x52 && // R
                              header[1] == 0x49 && // I
                              header[2] == 0x46 && // F
                              header[3] == 0x46 && // F
                                                   // Los siguientes 4 bytes son el tamaño, que puede variar
                              header[8] == 0x57 && // W
                              header[9] == 0x45 && // E
                              header[10] == 0x42 && // B
                              header[11] == 0x50;  // P
                }
                // HEIC/HEIF - estructura más compleja, verificamos "ftyp" seguido de identificadores comunes
                else if (normalizedContentType.Contains("heic") || normalizedContentType.Contains("heif"))
                {
                    // HEIC/HEIF tiene una estructura más compleja, pero podemos verificar algunos patrones comunes
                    // Nota: Esta es una verificación simplificada. Para una verificación completa se necesitaría analizar la estructura ISOBMFF
                    if (bytesRead >= 12)
                    {
                        // Buscar "ftyp" que generalmente está en la posición 4
                        for (int i = 0; i < bytesRead - 8; i++)
                        {
                            if (header[i] == 0x66 && // f
                                header[i + 1] == 0x74 && // t
                                header[i + 2] == 0x79 && // y
                                header[i + 3] == 0x70) // p
                            {
                                // Luego verificamos si hay identificadores comunes de HEIC/HEIF
                                if (i + 8 < bytesRead &&
                                    ((header[i + 4] == 0x68 && header[i + 5] == 0x65 && header[i + 6] == 0x69 && header[i + 7] == 0x63) || // "heic"
                                     (header[i + 4] == 0x68 && header[i + 5] == 0x65 && header[i + 6] == 0x69 && header[i + 7] == 0x78) || // "heix"
                                     (header[i + 4] == 0x6D && header[i + 5] == 0x69 && header[i + 6] == 0x66 && header[i + 7] == 0x31))) // "mif1"
                                {
                                    isValid = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                // PDF - verifica "%PDF-"
                else if (normalizedContentType.Contains("pdf"))
                {
                    isValid = bytesRead >= 5 &&
                              header[0] == 0x25 && // %
                              header[1] == 0x50 && // P
                              header[2] == 0x44 && // D
                              header[3] == 0x46 && // F
                              header[4] == 0x2D;   // -
                }
                // SVG - siendo un formato XML, verificamos "<?xml" o "<svg"
                else if (normalizedContentType.Contains("svg"))
                {
                    // Convertir los primeros bytes a ASCII para verificar las etiquetas XML
                    string headerStr = System.Text.Encoding.ASCII.GetString(header, 0, bytesRead).ToLowerInvariant();
                    isValid = headerStr.Contains("<?xml") || headerStr.Contains("<svg");
                }
                // TIFF - verifica "II*\0" o "MM\0*"
                else if (normalizedContentType.Contains("tiff"))
                {
                    isValid = bytesRead >= 4 &&
                              ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) || // II*\0 (little endian)
                               (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A));  // MM\0* (big endian)
                }
                // Para imágenes de dispositivos móviles que pueden tener formatos específicos
                // pero que se declaran como image/jpeg o image/png, damos algo de flexibilidad
                else if (normalizedContentType.StartsWith("image/"))
                {
                    // Intenta verificar si es alguno de los formatos de imagen conocidos
                    isValid = IsImageFile(header, bytesRead);
                }

                _logger.LogInformation("Verificación de contenido para {ContentType}: {IsValid}", contentType, isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando contenido del archivo con tipo {ContentType}", contentType);
                return false; // Si hay un error, rechazamos el archivo por seguridad
            }
            finally
            {
                // Restaurar la posición original del stream para no afectar a otros procesamientos
                try
                {
                    fileStream.Position = originalPosition;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo restaurar la posición del stream");
                }
            }
        }

        /// <summary>
        /// Método auxiliar que verifica si el archivo es una imagen reconocida,
        /// independientemente del tipo MIME declarado
        /// </summary>
        private bool IsImageFile(byte[] header, int bytesRead)
        {
            // JPEG
            if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG
            if (bytesRead >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return true;

            // GIF
            if (bytesRead >= 6 &&
                header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
                ((header[4] == 0x37 && header[5] == 0x61) || (header[4] == 0x39 && header[5] == 0x61)))
                return true;

            // BMP
            if (bytesRead >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                return true;

            // WEBP
            if (bytesRead >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            // HEIC/HEIF - verificación simplificada
            if (bytesRead >= 12)
            {
                for (int i = 0; i < bytesRead - 8; i++)
                {
                    if (header[i] == 0x66 && header[i + 1] == 0x74 && header[i + 2] == 0x79 && header[i + 3] == 0x70)
                    {
                        if (i + 8 < bytesRead &&
                            ((header[i + 4] == 0x68 && header[i + 5] == 0x65 && header[i + 6] == 0x69) || // "hei"
                             (header[i + 4] == 0x6D && header[i + 5] == 0x69 && header[i + 6] == 0x66))) // "mif"
                        {
                            return true;
                        }
                    }
                }
            }

            // TIFF
            if (bytesRead >= 4 &&
                ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
                 (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)))
                return true;

            // No es ningún formato de imagen reconocido
            return false;
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

        /// <summary>
        /// Guarda los datos binarios de un archivo al sistema de archivos
        /// </summary>
        /// <param name="fileData">Contenido binario del archivo</param>
        /// <param name="fileName">Nombre original del archivo</param>
        /// <param name="contentType">Tipo MIME del archivo</param>
        /// <param name="requestId">ID de la solicitud</param>
        /// <param name="itemId">ID del item</param>
        /// <returns>Ruta relativa donde se guardó el archivo</returns>
        public async Task<string> SaveFileDataAsync(byte[] fileData, string fileName, string contentType, int requestId, int itemId)
        {
            if (fileData == null || fileData.Length == 0)
                throw new ArgumentException("Los datos del archivo no pueden estar vacíos", nameof(fileData));

            if (fileData.Length > _maxFileSize)
            {
                throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido de {_maxFileSize / (1024 * 1024)}MB");
            }

            // Verificar tipo MIME
            if (!_allowedFileTypes.Contains(contentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException($"Tipo de archivo '{contentType}' no permitido");
            }

            // Sanitizar nombre de archivo
            SanitizeFileName(ref fileName);

            if (!IsValidFileExtension(fileName))
            {
                throw new InvalidOperationException("Tipo de archivo no permitido");
            }

            // Crear directorios si no existen
            string uploadPath = Path.Combine(_environment.ContentRootPath, "uploads", requestId.ToString(), itemId.ToString());
            Directory.CreateDirectory(uploadPath);

            // Generar nombre único para evitar colisiones y ofuscación adicional
            string uniqueFileName = $"{Path.GetRandomFileName()}_{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            string filePath = Path.Combine(uploadPath, uniqueFileName);

            // Escribir directamente el array de bytes al sistema de archivos
            await File.WriteAllBytesAsync(filePath, fileData);

            // Devolver la ruta relativa para guardar en la base de datos
            return $"uploads/{requestId}/{itemId}/{uniqueFileName}";
        }
    }

}