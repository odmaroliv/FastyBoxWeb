using FastyBoxWeb.Data.Enums;
using FastyBoxWeb.Services.Payment;
using FastyBoxWeb.Services.Shipping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace FastyBoxWeb.Controllers
{
    // Cambiado de /api/stripe/webhook a /api/webhooks/stripe para coincidir con la URL en logs
    [Route("api/webhooks/stripe")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly StripeSettings _stripeSettings;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly IForwardingService _forwardingService;

        public StripeWebhookController(
            IPaymentService paymentService,
            IOptions<StripeSettings> stripeOptions,
            ILogger<StripeWebhookController> logger,
            IForwardingService forwardingService)
        {
            _paymentService = paymentService;
            _stripeSettings = stripeOptions.Value;
            _logger = logger;
            _forwardingService = forwardingService;
        }

        [HttpPost]
        public async Task<IActionResult> HandleWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            // Registro del payload raw para debugging
            _logger.LogDebug("Webhook payload received: {Payload}", json);

            try
            {
                // Obtenemos el encabezado Stripe-Signature
                var signatureHeader = Request.Headers["Stripe-Signature"];
                _logger.LogDebug("Webhook signature: {Signature}", signatureHeader);

                if (string.IsNullOrEmpty(signatureHeader))
                {
                    _logger.LogWarning("Webhook received without Stripe-Signature header");
                    return BadRequest("No Stripe signature found in request");
                }

                // Registrar el secreto de webhook que se está usando (parcialmente oculto)
                var partialSecret = _stripeSettings.WebhookSecret?.Length > 4
                    ? $"{_stripeSettings.WebhookSecret.Substring(0, 4)}..."
                    : "not set";
                _logger.LogDebug("Using webhook secret starting with: {Secret}", partialSecret);

                // Construir el evento con verificación de firma
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signatureHeader,
                    _stripeSettings.WebhookSecret
                );

                _logger.LogInformation("Received Stripe webhook event: {EventType} with ID: {EventId}",
                    stripeEvent.Type, stripeEvent.Id);

                // Handle the event based on its type
                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                        await HandleCheckoutSessionCompleted(session);
                        break;

                    case "payment_intent.succeeded":
                        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentSucceeded(paymentIntent);
                        break;

                    case "payment_intent.payment_failed":
                        var failedPaymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentFailed(failedPaymentIntent);
                        break;

                    // Puedes agregar más manejadores de eventos según necesites
                    default:
                        _logger.LogInformation("Unhandled event type: {EventType}", stripeEvent.Type);
                        break;
                }

                return Ok();
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "Error validating Stripe webhook: {Message}", e.Message);
                return BadRequest(new { error = e.Message });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing Stripe webhook: {Message}", e.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private async Task HandleCheckoutSessionCompleted(Stripe.Checkout.Session session)
        {
            if (session == null)
            {
                _logger.LogWarning("Received null session in HandleCheckoutSessionCompleted");
                return;
            }

            _logger.LogInformation("Processing completed checkout session: {SessionId}, PaymentStatus: {PaymentStatus}",
                session.Id, session.PaymentStatus);

            if (session.PaymentStatus == "paid")
            {
                // Update payment status to succeeded
                var payment = await _paymentService.RecordPaymentAsync(
                    session.Id,
                    session.PaymentIntentId,
                    PaymentStatus.Succeeded);

                _logger.LogInformation("Payment {SessionId} completed successfully", session.Id);

                // Añadir esta sección para actualizar el estado de la solicitud si el pago está completo
                if (payment != null && payment.ForwardRequestId > 0) // Cambiado de HasValue a > 0
                {
                    await UpdateRequestStatusIfPaidInFull(payment.ForwardRequestId); // Quitado .Value
                }
            }
            else
            {
                _logger.LogWarning("Checkout session completed but payment status is {PaymentStatus}",
                    session.PaymentStatus);
            }
        }


        private async Task HandlePaymentIntentSucceeded(PaymentIntent paymentIntent)
        {
            if (paymentIntent == null)
            {
                _logger.LogWarning("Received null paymentIntent in HandlePaymentIntentSucceeded");
                return;
            }

            _logger.LogInformation("Processing successful payment intent: {PaymentIntentId}", paymentIntent.Id);

            var payment = await _paymentService.GetPaymentByTransactionIdAsync(paymentIntent.Id);

            if (payment != null)
            {
                // Update payment status
                await _paymentService.UpdatePaymentStatusAsync(paymentIntent.Id, PaymentStatus.Succeeded);
                _logger.LogInformation("Payment {PaymentIntentId} succeeded", paymentIntent.Id);

                // Añadir esta sección para actualizar el estado de la solicitud si el pago está completo
                if (payment.ForwardRequestId > 0) // Cambiado de HasValue a > 0
                {
                    await UpdateRequestStatusIfPaidInFull(payment.ForwardRequestId); // Quitado .Value
                }
            }
            else
            {
                _logger.LogWarning("Payment intent {PaymentIntentId} succeeded but no matching payment found in database",
                    paymentIntent.Id);
            }
        }



        private async Task UpdateRequestStatusIfPaidInFull(int requestId)
        {
            try
            {
                var request = await _paymentService.GetRequestWithPaymentsAsync(requestId);

                if (request != null && request.Status == ForwardRequestStatus.AwaitingPayment)
                {
                    // Verificar si el pago está completo (el total pagado es mayor o igual al total final)
                    if (request.TotalPaid >= request.FinalTotal)
                    {
                        _logger.LogInformation("Request {RequestId} is now fully paid. Updating status to Processing.", requestId);

                        // Actualizar al siguiente estado (Processing)
                        await _forwardingService.UpdateRequestStatusAsync(
                            requestId,
                            ForwardRequestStatus.Processing,
                            "Automatically moved to processing after payment completed",
                            request.UserId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request status after payment: {Message}", ex.Message);
            }
        }

        private async Task HandlePaymentIntentFailed(PaymentIntent paymentIntent)
        {
            if (paymentIntent == null)
            {
                _logger.LogWarning("Received null paymentIntent in HandlePaymentIntentFailed");
                return;
            }

            _logger.LogInformation("Processing failed payment intent: {PaymentIntentId}", paymentIntent.Id);

            var payment = await _paymentService.GetPaymentByTransactionIdAsync(paymentIntent.Id);

            if (payment != null)
            {
                // Update payment status
                await _paymentService.UpdatePaymentStatusAsync(paymentIntent.Id, PaymentStatus.Failed);
                _logger.LogInformation("Payment {PaymentIntentId} failed", paymentIntent.Id);
            }
            else
            {
                _logger.LogWarning("Payment intent {PaymentIntentId} failed but no matching payment found in database",
                    paymentIntent.Id);
            }
        }
    }
}