// Controllers/StripeApiController.cs
using FastyBoxWeb.Services.Payment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FastyBoxWeb.Controllers
{
    [Route("api/stripe")]
    [ApiController]
    public class StripeApiController : ControllerBase
    {
        private readonly IOptions<StripeSettings> _stripeOptions;

        public StripeApiController(IOptions<StripeSettings> stripeOptions)
        {
            _stripeOptions = stripeOptions;
        }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                publishableKey = _stripeOptions.Value.PublishableKey
            });
        }
    }
}