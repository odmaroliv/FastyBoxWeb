using FastyBoxWeb.Services.Payment;
using Microsoft.AspNetCore.Mvc;

namespace FastyBoxWeb.Controllers
{
    [Route("[controller]")]
    public class PaymentController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet("success")]
        public async Task<IActionResult> Success([FromQuery] string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                _logger.LogWarning("Payment success callback received without session_id");
                return Redirect("/dashboard?error=payment_invalid");
            }

            try
            {
                // Buscar el pago en la base de datos
                var payment = await _paymentService.GetPaymentByTransactionIdAsync(session_id);
                if (payment == null)
                {
                    _logger.LogWarning($"Payment with session ID {session_id} not found");
                    return Redirect("/dashboard?error=payment_not_found");
                }

                // Actualizar el estado del pago si aún no se ha actualizado por webhook
                if (payment.Status == Data.Enums.PaymentStatus.Pending)
                {
                    _logger.LogInformation($"Updating pending payment status for session {session_id}");
                    await _paymentService.UpdatePaymentStatusAsync(session_id, Data.Enums.PaymentStatus.Succeeded);
                }

                // Redireccionar a la página de detalles de la solicitud
                _logger.LogInformation($"Redirecting to request details for request ID {payment.ForwardRequestId}");
                return Redirect($"/request/{payment.ForwardRequestId}?success=payment_completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing successful payment with session ID {session_id}");
                return Redirect("/dashboard?error=payment_error");
            }
        }

        [HttpGet("cancel")]
        public IActionResult Cancel([FromQuery] string session_id)
        {
            // Log the cancellation
            _logger.LogInformation($"Payment with session ID {session_id} was cancelled by the user");

            // Redirect to dashboard with a cancellation parameter
            return Redirect("/dashboard?status=payment_cancelled");
        }
    }
}