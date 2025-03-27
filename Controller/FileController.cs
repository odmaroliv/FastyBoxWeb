
using FastyBoxWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FastyBoxWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FileController> _logger;
        private readonly IWebHostEnvironment _environment;

        public FileController(
            ApplicationDbContext context,
            ILogger<FileController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                // Obtener el ID del usuario actual
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Buscar el adjunto
                var attachment = await _context.Attachments
                    .Include(a => a.ForwardItem)
                    .ThenInclude(i => i.ForwardRequest)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (attachment == null)
                {
                    return NotFound();
                }

                // Verificar si el usuario tiene acceso (es propietario o administrador)
                var isAdmin = User.IsInRole("Administrator");
                if (!isAdmin && attachment.ForwardItem.ForwardRequest.UserId != userId)
                {
                    return Forbid();
                }

                // Construir la ruta completa
                var fullPath = Path.Combine(_environment.ContentRootPath, attachment.FilePath);

                // Verificar que el archivo existe
                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning($"Archivo no encontrado: {fullPath}");
                    return NotFound("Archivo no encontrado");
                }

                // Leer el archivo y devolverlo
                var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                return File(fileBytes, attachment.ContentType, attachment.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener archivo");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
}