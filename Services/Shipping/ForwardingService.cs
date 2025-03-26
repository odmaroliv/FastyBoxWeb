using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Entities;
using FastyBoxWeb.Data.Enums;
using FastyBoxWeb.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace FastyBoxWeb.Services.Shipping
{
    public interface IForwardingService
    {
        Task<ForwardRequest> CreateRequestAsync(ForwardRequest request);
        Task<ForwardRequest> UpdateRequestAsync(ForwardRequest request);
        Task<ForwardRequest?> GetRequestByIdAsync(int id, string userId, bool isAdmin = false);
        Task<List<ForwardRequest>> GetUserRequestsAsync(string userId, int page = 1, int pageSize = 10);
        Task<List<ForwardRequest>> GetAllRequestsAsync(int page = 1, int pageSize = 10, ForwardRequestStatus? status = null);
        Task<bool> UpdateRequestStatusAsync(int requestId, ForwardRequestStatus status, string notes, string userId);
        Task<bool> AssignShippingAddressAsync(int requestId, int addressId, string userId);
        Task<ForwardItem> AddItemToRequestAsync(int requestId, ForwardItem item, string userId);
        Task<bool> RemoveItemFromRequestAsync(int requestId, int itemId, string userId);
        Task<bool> DeleteRequestAsync(int requestId, string userId, bool isAdmin = false);
    }

    public class ForwardingService : IForwardingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IShippingCalculatorService _shippingCalculator;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ForwardingService> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public ForwardingService(
            ApplicationDbContext context,
            IShippingCalculatorService shippingCalculator,
            INotificationService notificationService,
            ILogger<ForwardingService> logger)
        {
            _context = context;
            _shippingCalculator = shippingCalculator;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<ForwardRequest> CreateRequestAsync(ForwardRequest request)
        {
            // Limpiar cualquier entrada inválida
            SanitizeRequest(request);

            // Generar código de seguimiento único (con control de concurrencia)
            await _semaphore.WaitAsync();
            try
            {
                request.TrackingCode = await GenerateUniqueTrackingCodeAsync();

                // Calcular total estimado
                request.EstimatedTotal = await _shippingCalculator.CalculateEstimatedTotalAsync(request);

                // Registrar historial de estado
                request.StatusHistory.Add(new RequestStatusHistory
                {
                    Status = request.Status,
                    Notes = "Solicitud creada"
                });

                await _context.ForwardRequests.AddAsync(request);
                await _context.SaveChangesAsync();

                // Enviar notificación (de forma asíncrona, no bloquear la respuesta)
                _ = _notificationService.SendRequestCreatedNotificationAsync(request)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogError(t.Exception, "Error al enviar notificación de creación");
                    });

                return request;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear solicitud de reenvío");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ForwardRequest> UpdateRequestAsync(ForwardRequest request)
        {
            // Limpiar cualquier entrada inválida
            SanitizeRequest(request);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingRequest = await _context.ForwardRequests
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == request.Id);

                if (existingRequest == null)
                {
                    throw new KeyNotFoundException($"No se encontró la solicitud con ID {request.Id}");
                }

                // Actualizar propiedades
                existingRequest.Notes = request.Notes;
                existingRequest.ShippingAddressId = request.ShippingAddressId;
                existingRequest.OriginalCarrier = request.OriginalCarrier;
                existingRequest.OriginalTrackingNumber = request.OriginalTrackingNumber;

                // Recalcular total estimado
                existingRequest.EstimatedTotal = await _shippingCalculator.CalculateEstimatedTotalAsync(existingRequest);

                _context.ForwardRequests.Update(existingRequest);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return existingRequest;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al actualizar solicitud de reenvío");
                throw;
            }
        }

        public async Task<ForwardRequest?> GetRequestByIdAsync(int id, string userId, bool isAdmin = false)
        {
            var query = _context.ForwardRequests
                .Include(r => r.Items)
                .ThenInclude(i => i.Attachments)
                .Include(r => r.ShippingAddress)
                .Include(r => r.Payments)
                .Include(r => r.StatusHistory)
                .Include(r => r.User)
                .AsSplitQuery();




            if (!isAdmin)
            {
                query = query.Where(r => r.UserId == userId);
            }

            return await query.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<ForwardRequest>> GetUserRequestsAsync(string userId, int page = 1, int pageSize = 10)
        {
            // Validar parámetros
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50; // Limitar máximo

            return await _context.ForwardRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(r => r.Items)
                .Include(r => r.ShippingAddress)
                .ToListAsync();
        }

        public async Task<List<ForwardRequest>> GetAllRequestsAsync(int page = 1, int pageSize = 10, ForwardRequestStatus? status = null)
        {
            // Validar parámetros
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50; // Limitar máximo

            var query = _context.ForwardRequests.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(r => r.Items)
                .Include(r => r.ShippingAddress)
                .Include(r => r.User)
                .ToListAsync();
        }

        public async Task<bool> UpdateRequestStatusAsync(int requestId, ForwardRequestStatus status, string notes, string userId)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var request = await _context.ForwardRequests
                        .Include(r => r.StatusHistory)
                        .FirstOrDefaultAsync(r => r.Id == requestId);

                    if (request == null)
                    {
                        return false;
                    }

                    // Actualizar estado
                    request.Status = status;

                    // Registrar en historial
                    request.StatusHistory.Add(new RequestStatusHistory
                    {
                        Status = status,
                        Notes = notes,
                        CreatedBy = userId
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Enviar notificación (de forma asíncrona)
                    _ = _notificationService.SendStatusUpdateNotificationAsync(request)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                _logger.LogError(t.Exception, "Error al enviar notificación de actualización de estado");
                        });

                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error al actualizar estado de solicitud");
                    return false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> AssignShippingAddressAsync(int requestId, int addressId, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.ForwardRequests
                    .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

                if (request == null)
                {
                    return false;
                }

                var address = await _context.Addresses
                    .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

                if (address == null)
                {
                    return false;
                }

                request.ShippingAddressId = addressId;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al asignar dirección de envío");
                return false;
            }
        }

        public async Task<ForwardItem> AddItemToRequestAsync(int requestId, ForwardItem item, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.ForwardRequests
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

                if (request == null)
                {
                    throw new KeyNotFoundException($"No se encontró la solicitud con ID {requestId}");
                }

                // Sanitizar datos del item
                SanitizeItem(item);

                item.ForwardRequestId = requestId;
                await _context.ForwardItems.AddAsync(item);

                // Recalcular total estimado
                request.EstimatedTotal = await _shippingCalculator.CalculateEstimatedTotalAsync(request);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return item;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al agregar ítem a la solicitud");
                throw;
            }
        }

        public async Task<bool> RemoveItemFromRequestAsync(int requestId, int itemId, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.ForwardRequests
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

                if (request == null)
                {
                    return false;
                }

                var item = await _context.ForwardItems
                    .Include(i => i.Attachments)
                    .FirstOrDefaultAsync(i => i.Id == itemId && i.ForwardRequestId == requestId);

                if (item == null)
                {
                    return false;
                }

                // Primero eliminar los archivos adjuntos
                if (item.Attachments.Any())
                {
                    _context.Attachments.RemoveRange(item.Attachments);
                }

                // Luego eliminar el item
                _context.ForwardItems.Remove(item);

                // Recalcular total estimado
                request.EstimatedTotal = await _shippingCalculator.CalculateEstimatedTotalAsync(request);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar ítem de la solicitud");
                return false;
            }
        }

        public async Task<bool> DeleteRequestAsync(int requestId, string userId, bool isAdmin = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.ForwardRequests
                    .Include(r => r.Items).ThenInclude(i => i.Attachments)
                    .Include(r => r.Payments)
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == requestId && (isAdmin || r.UserId == userId));

                if (request == null)
                {
                    return false;
                }

                // Eliminar todos los elementos relacionados para evitar conflictos de clave foránea
                foreach (var item in request.Items)
                {
                    if (item.Attachments.Any())
                    {
                        _context.Attachments.RemoveRange(item.Attachments);
                    }
                }
                _context.ForwardItems.RemoveRange(request.Items);
                _context.Payments.RemoveRange(request.Payments);
                _context.RequestStatusHistories.RemoveRange(request.StatusHistory);

                // Finalmente eliminar la solicitud
                _context.ForwardRequests.Remove(request);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar solicitud");
                return false;
            }
        }

        private async Task<string> GenerateUniqueTrackingCodeAsync()
        {
            bool isUnique = false;
            string trackingCode = "";
            int attempts = 0;

            // Formato: FB-YYYYMMDD-XXXXX
            var dateCode = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random();

            while (!isUnique && attempts < 10)
            {
                var randomPart = random.Next(10000, 99999).ToString();
                trackingCode = $"FB-{dateCode}-{randomPart}";

                // Verificar si el código ya existe
                var exists = await _context.ForwardRequests
                    .AnyAsync(r => r.TrackingCode == trackingCode);

                isUnique = !exists;
                attempts++;
            }

            if (!isUnique)
            {
                throw new InvalidOperationException("No se pudo generar un código de seguimiento único después de varios intentos");
            }

            return trackingCode;
        }

        private void SanitizeRequest(ForwardRequest request)
        {
            // Aplicar limpieza y validación básica de campos
            request.OriginalCarrier = SanitizeString(request.OriginalCarrier, 100);
            request.OriginalTrackingNumber = SanitizeString(request.OriginalTrackingNumber, 100);
            request.Notes = SanitizeString(request.Notes, 250);
        }

        private void SanitizeItem(ForwardItem item)
        {
            // Aplicar limpieza y validación básica de campos
            item.Name = SanitizeString(item.Name, 200);
            item.Url = SanitizeString(item.Url, 500);
            item.Vendor = SanitizeString(item.Vendor, 150);
            item.Notes = SanitizeString(item.Notes, 500);

            // Validar valores numéricos
            if (item.DeclaredValue < 0) item.DeclaredValue = 0;
            if (item.DeclaredWeight.HasValue && item.DeclaredWeight < 0) item.DeclaredWeight = 0;
            if (item.DeclaredLength.HasValue && item.DeclaredLength < 0) item.DeclaredLength = 0;
            if (item.DeclaredWidth.HasValue && item.DeclaredWidth < 0) item.DeclaredWidth = 0;
            if (item.DeclaredHeight.HasValue && item.DeclaredHeight < 0) item.DeclaredHeight = 0;
        }

        private string SanitizeString(string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Eliminar caracteres potencialmente peligrosos
            var sanitized = input.Trim();

            // Limitar longitud
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized;
        }
    }
}