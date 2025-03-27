using FastyBoxWeb.Data.Enums;
using FastyBoxWeb.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FastyBoxWeb.Controllers
{
    [Route("api/stripe")]
    [ApiController]
    public class StripeApiController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IOptions<StripeSettings> _stripeOptions;
        private readonly ILogger<StripeApiController> _logger;

        public StripeApiController(
            IPaymentService paymentService,
            IOptions<StripeSettings> stripeOptions,
            ILogger<StripeApiController> logger)
        {
            _paymentService = paymentService;
            _stripeOptions = stripeOptions;
            _logger = logger;
        }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                publishableKey = _stripeOptions.Value.PublishableKey
            });
        }

        [HttpPost("create-checkout-session")]
        [Authorize]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutRequest request)
        {
            if (request == null || request.RequestId <= 0 || request.Amount <= 0)
            {
                return BadRequest("Invalid request parameters");
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var session = await _paymentService.CreateCheckoutSessionAsync(
                    request.RequestId,
                    request.Amount,
                    request.PaymentType,
                    userId);

                return Ok(new
                {
                    sessionId = session.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout session");
                return StatusCode(500, "Error creating checkout session");
            }
        }

        [HttpGet("payment/{sessionId}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentStatus(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest("Session ID is required");
            }

            try
            {
                var payment = await _paymentService.GetPaymentByTransactionIdAsync(sessionId);
                if (payment == null)
                {
                    return NotFound("Payment not found");
                }

                // Check if the payment belongs to the current user
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (payment.UserId != userId && !User.IsInRole("Administrator"))
                {
                    return Forbid();
                }

                return Ok(new
                {
                    status = payment.Status.ToString(),
                    amount = payment.Amount,
                    date = payment.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment status");
                return StatusCode(500, "Error getting payment status");
            }
        }
    }

    public class CreateCheckoutRequest
    {
        public int RequestId { get; set; }
        public decimal Amount { get; set; }
        public PaymentType PaymentType { get; set; } = PaymentType.Additional;
    }
}