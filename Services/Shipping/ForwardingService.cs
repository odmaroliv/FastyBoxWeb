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

        public ForwardingService(
            ApplicationDbContext context,
            IShippingCalculatorService shippingCalculator,
            INotificationService notificationService)
        {
            _context = context;
            _shippingCalculator = shippingCalculator;
            _notificationService = notificationService;
        }

        public async Task<ForwardRequest> CreateRequestAsync(ForwardRequest request)
        {
            // Generar código de seguimiento único
            request.TrackingCode = GenerateTrackingCode();

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

            // Enviar notificación
            await _notificationService.SendRequestCreatedNotificationAsync(request);

            return request;
        }

        public async Task<ForwardRequest> UpdateRequestAsync(ForwardRequest request)
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

            return existingRequest;
        }

        public async Task<ForwardRequest?> GetRequestByIdAsync(int id, string userId, bool isAdmin = false)
        {
            var query = _context.ForwardRequests
                .Include(r => r.Items)
                .ThenInclude(i => i.Attachments)
                .Include(r => r.ShippingAddress)
                .Include(r => r.Payments)
                .Include(r => r.StatusHistory)
                .AsSplitQuery();

            if (!isAdmin)
            {
                query = query.Where(r => r.UserId == userId);
            }

            return await query.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<ForwardRequest>> GetUserRequestsAsync(string userId, int page = 1, int pageSize = 10)
        {
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
            var request = await _context.ForwardRequests.FindAsync(requestId);
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

            // Enviar notificación
            await _notificationService.SendStatusUpdateNotificationAsync(request);

            return true;
        }

        public async Task<bool> AssignShippingAddressAsync(int requestId, int addressId, string userId)
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

            return true;
        }

        public async Task<ForwardItem> AddItemToRequestAsync(int requestId, ForwardItem item, string userId)
        {
            var request = await _context.ForwardRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

            if (request == null)
            {
                throw new KeyNotFoundException($"No se encontró la solicitud con ID {requestId}");
            }

            item.ForwardRequestId = requestId;
            await _context.ForwardItems.AddAsync(item);

            // Recalcular total estimado
            request.EstimatedTotal = await _shippingCalculator.CalculateEstimatedTotalAsync(request);

            await _context.SaveChangesAsync();

            return item;
        }

        public async Task<bool> RemoveItemFromRequestAsync(int requestId, int itemId, string userId)
        {
            var request = await _context.ForwardRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

            if (request == null)
            {
                return false;
            }

            var item = await _context.ForwardItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.ForwardRequestId == requestId);

            if (item == null)
            {
                return false;
            }

            _context.ForwardItems.Remove(item);

            // Recalcular total estimado
            request.EstimatedTotal = await _shippingCalculator.CalculateEstimatedTotalAsync(request);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteRequestAsync(int requestId, string userId, bool isAdmin = false)
        {
            var request = await _context.ForwardRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && (isAdmin || r.UserId == userId));

            if (request == null)
            {
                return false;
            }

            _context.ForwardRequests.Remove(request);
            await _context.SaveChangesAsync();

            return true;
        }

        private string GenerateTrackingCode()
        {
            // Formato: FB-YYYYMMDD-XXXXX
            var dateCode = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random();
            var randomPart = random.Next(10000, 99999).ToString();

            return $"FB-{dateCode}-{randomPart}";
        }
    }
}

