using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Entities;
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
        Task<ForwardRequest> GetRequestWithPaymentsAsync(int requestId);
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

            // Usar URLs configuradas directamente para evitar problemas
            string successUrl = EnsureValidUrl(_stripeSettings.SuccessUrl);
            string cancelUrl = EnsureValidUrl(_stripeSettings.CancelUrl);

            _logger.LogInformation($"URLs de redirección: Success={successUrl}, Cancel={cancelUrl}");

            try
            {
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
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl,
                    ClientReferenceId = requestId.ToString(),
                    Metadata = new Dictionary<string, string>
                    {
                        { "RequestId", requestId.ToString() },
                        { "UserId", userId },
                        { "PaymentType", ((int)type).ToString() }
                    }
                };

                // Obtener email del usuario si está disponible
                var userEmail = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(userEmail))
                {
                    options.CustomerEmail = userEmail;
                }

                // Crear sesión de pago
                var service = new Stripe.Checkout.SessionService();
                var session = await service.CreateAsync(options);

                _logger.LogInformation($"Sesión de Stripe creada: {session.Id} para solicitud {requestId}");

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
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"Error de Stripe al crear sesión para solicitud {requestId}");
                throw new Exception($"Error de Stripe: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al crear sesión de pago para solicitud {requestId}");
                throw;
            }
        }

        public async Task<ForwardRequest> GetRequestWithPaymentsAsync(int requestId)
        {
            return await _context.ForwardRequests
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == requestId);
        }

        // Método para garantizar que la URL sea válida
        private string EnsureValidUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                // Usar una URL de fallback en caso de que la configurada sea nula o vacía
                return "https://localhost:5001/payment/success?session_id={CHECKOUT_SESSION_ID}";
            }

            // Verificar que la URL sea absoluta y válida
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning($"URL no válida en configuración: {url}, usando URL fallback");
                return "https://localhost:5001/payment/success?session_id={CHECKOUT_SESSION_ID}";
            }

            return url;
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


            // Actualizar el estado del pago
            payment.Status = status;
            payment.ModifiedAt = DateTime.UtcNow;
            payment.ModifiedBy = "System";

            _context.Update(payment);
            await _context.SaveChangesAsync();

            // Si el pago es exitoso, verificar si completa el total de la solicitud
            if (status == PaymentStatus.Succeeded && payment.ForwardRequestId > 0)
            {
                await UpdateRequestStatusIfPaidInFull(payment.ForwardRequestId);
            }


            payment.Status = status;
            await _context.SaveChangesAsync();

            return payment;
        }

        private async Task UpdateRequestStatusIfPaidInFull(int requestId)
        {
            try
            {
                // Obtener la solicitud con todos sus pagos
                var request = await _context.ForwardRequests
                    .Include(r => r.Payments)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request != null && request.Status == ForwardRequestStatus.AwaitingPayment)
                {
                    // Calcular el total pagado (considerando solo pagos exitosos)
                    decimal totalPaid = request.Payments
                        .Where(p => p.Status == PaymentStatus.Succeeded)
                        .Sum(p => p.Amount);

                    // Verificar si el pago está completo
                    if (totalPaid >= request.FinalTotal)
                    {
                        // Cambiar el estado de la solicitud a Processing
                        request.Status = ForwardRequestStatus.Processing;
                        request.ModifiedAt = DateTime.UtcNow;
                        request.ModifiedBy = "System";

                        // Agregar entrada en el historial de estados
                        request.StatusHistory.Add(new RequestStatusHistory
                        {
                            Status = ForwardRequestStatus.Processing,
                            Notes = "Automatically moved to processing after payment completed",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        });

                        _context.Update(request);
                        await _context.SaveChangesAsync();

                        // Aquí podrías agregar código para notificar al usuario si es necesario
                    }
                }
            }
            catch (Exception ex)
            {
                // Registrar el error pero no propagar la excepción para no interrumpir el proceso de pago
                _logger.LogError(ex, "Error updating request status after payment: {Message}", ex.Message);
            }
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