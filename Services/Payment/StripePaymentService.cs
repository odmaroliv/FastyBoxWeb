using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Enums;
using FastyBoxWeb.Services.Notification;
using FastyBoxWeb.Services.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace FastyBoxWeb.Services.Payment
{
    public interface IPaymentService
    {
        Task<Stripe.Checkout.Session> CreateCheckoutSessionAsync(int requestId, decimal amount, PaymentType type, string userId);
        Task<Data.Entities.Payment> RecordPaymentAsync(string sessionId, string paymentIntentId, PaymentStatus status);
        Task<string> GetClientSecretAsync(int requestId, decimal amount, PaymentType type, string userId);
        Task<Data.Entities.Payment?> GetPaymentByTransactionIdAsync(string transactionId);
        Task<Data.Entities.Payment> UpdatePaymentStatusAsync(string transactionId, PaymentStatus status);
        Task<List<Data.Entities.Payment>> GetPaymentsForRequestAsync(int requestId);
    }

    public class StripePaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly StripeSettings _stripeSettings;
        private readonly IForwardingService _forwardingService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<StripePaymentService> _logger;

        public StripePaymentService(
            ApplicationDbContext context,
            IOptions<StripeSettings> stripeSettings,
            IForwardingService forwardingService,
            INotificationService notificationService,
            ILogger<StripePaymentService> logger)
        {
            _context = context;
            _stripeSettings = stripeSettings.Value;
            _forwardingService = forwardingService;
            _notificationService = notificationService;
            _logger = logger;

            // Configurar Stripe
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        public async Task<Stripe.Checkout.Session> CreateCheckoutSessionAsync(int requestId, decimal amount, PaymentType type, string userId)
        {
            // Verificar que la solicitud exista y pertenezca al usuario
            var request = await _context.ForwardRequests
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

            if (request == null)
            {
                throw new KeyNotFoundException($"No se encontró la solicitud con ID {requestId} para el usuario actual");
            }

            // Crear una descripción para la sesión
            var description = $"Pago para envío #{request.TrackingCode} - " + type switch
            {
                PaymentType.Initial => "Pago inicial",
                PaymentType.Additional => "Pago adicional",
                PaymentType.Complete => "Pago completo",
                _ => "Pago"
            };

            // Convertir a centavos para Stripe
            var amountInCents = (long)(amount * 100);

            // Crear opciones de sesión
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                {
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                        {
                            UnitAmount = amountInCents,
                            Currency = _stripeSettings.Currency,
                            ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"FastyBox Envío #{request.TrackingCode}",
                                Description = description
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"{_stripeSettings.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = _stripeSettings.CancelUrl,
                ClientReferenceId = requestId.ToString(),
                CustomerEmail = _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefault(),
                Metadata = new Dictionary<string, string>
                {
                    { "RequestId", requestId.ToString() },
                    { "UserId", userId },
                    { "PaymentType", ((int)type).ToString() }
                }
            };

            // Crear sesión de pago
            var service = new Stripe.Checkout.SessionService();
            var session = await service.CreateAsync(options);

            // Registrar el pago en nuestra base de datos
            var payment = new Data.Entities.Payment
            {
                ForwardRequestId = requestId,
                UserId = userId,
                Amount = amount,
                Status = PaymentStatus.Pending,
                Type = type,
                TransactionId = session.Id,
                PaymentMethod = "Stripe",
                Notes = description
            };

            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            return session;
        }

        public async Task<string> GetClientSecretAsync(int requestId, decimal amount, PaymentType type, string userId)
        {
            // Verificar que la solicitud exista y pertenezca al usuario
            var request = await _context.ForwardRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

            if (request == null)
            {
                throw new KeyNotFoundException($"No se encontró la solicitud con ID {requestId} para el usuario actual");
            }

            // Convertir a centavos para Stripe
            var amountInCents = (long)(amount * 100);

            // Crear un PaymentIntent
            var options = new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = _stripeSettings.Currency,
                Description = $"Pago para envío #{request.TrackingCode}",
                Metadata = new Dictionary<string, string>
                {
                    { "RequestId", requestId.ToString() },
                    { "UserId", userId },
                    { "PaymentType", ((int)type).ToString() }
                }
            };

            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options);

            // Registrar el pago en nuestra base de datos
            var payment = new Data.Entities.Payment
            {
                ForwardRequestId = requestId,
                UserId = userId,
                Amount = amount,
                Status = PaymentStatus.Pending,
                Type = type,
                TransactionId = intent.Id,
                PaymentMethod = "Stripe",
                Notes = $"Pago Intent para envío #{request.TrackingCode}"
            };

            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            // Devolver el clientSecret para que el frontend pueda completar el pago
            return intent.ClientSecret;
        }

        public async Task<Data.Entities.Payment> RecordPaymentAsync(string sessionId, string paymentIntentId, PaymentStatus status)
        {
            // Buscar el pago existente por sessionId
            var payment = await _context.Payments
                .Include(p => p.ForwardRequest)
                .FirstOrDefaultAsync(p => p.TransactionId == sessionId);

            if (payment == null)
            {
                _logger.LogWarning($"No se encontró pago con sessionId {sessionId}");

                // Buscar por paymentIntentId como fallback
                payment = await _context.Payments
                    .Include(p => p.ForwardRequest)
                    .FirstOrDefaultAsync(p => p.TransactionId == paymentIntentId);

                if (payment == null)
                {
                    throw new KeyNotFoundException($"No se encontró pago con sessionId {sessionId} o paymentIntentId {paymentIntentId}");
                }
            }

            // Actualizar el estado del pago
            payment.Status = status;

            // Si el pago fue exitoso, actualizar también el paymentIntentId
            if (status == PaymentStatus.Succeeded && !string.IsNullOrEmpty(paymentIntentId))
            {
                payment.TransactionId = paymentIntentId;
            }

            // Si el pago fue exitoso, actualizar el estado de la solicitud si es necesario
            if (status == PaymentStatus.Succeeded && payment.ForwardRequest != null)
            {
                // Si es un pago inicial, cambiar el estado a AwaitingArrival
                if (payment.Type == PaymentType.Initial &&
                    payment.ForwardRequest.Status == ForwardRequestStatus.Draft)
                {
                    await _forwardingService.UpdateRequestStatusAsync(
                        payment.ForwardRequestId,
                        ForwardRequestStatus.AwaitingArrival,
                        "Pago inicial recibido",
                        payment.UserId);
                }
                // Si es un pago adicional o completo y el estado es AwaitingPayment, cambiar a Processing
                else if ((payment.Type == PaymentType.Additional || payment.Type == PaymentType.Complete) &&
                         payment.ForwardRequest.Status == ForwardRequestStatus.AwaitingPayment)
                {
                    await _forwardingService.UpdateRequestStatusAsync(
                        payment.ForwardRequestId,
                        ForwardRequestStatus.Processing,
                        "Pago completo recibido",
                        payment.UserId);
                }
            }

            await _context.SaveChangesAsync();

            // Enviar notificación de confirmación de pago - Fix parameter type issue
            await _notificationService.SendPaymentConfirmationAsync(payment);

            return payment;
        }

        public async Task<Data.Entities.Payment?> GetPaymentByTransactionIdAsync(string transactionId)
        {
            return await _context.Payments
                .Include(p => p.ForwardRequest)
                .FirstOrDefaultAsync(p => p.TransactionId == transactionId);
        }

        public async Task<Data.Entities.Payment> UpdatePaymentStatusAsync(string transactionId, PaymentStatus status)
        {
            var payment = await GetPaymentByTransactionIdAsync(transactionId);
            if (payment == null)
            {
                throw new KeyNotFoundException($"No se encontró pago con transactionId {transactionId}");
            }

            payment.Status = status;
            await _context.SaveChangesAsync();

            return payment;
        }

        public async Task<List<Data.Entities.Payment>> GetPaymentsForRequestAsync(int requestId)
        {
            return await _context.Payments
                .Where(p => p.ForwardRequestId == requestId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}