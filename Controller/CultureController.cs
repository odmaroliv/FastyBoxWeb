using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace FastyBoxWeb.Controller
{
    [Route("[controller]/[action]")]
    public class CultureController : Microsoft.AspNetCore.Mvc.Controller
    {
        public IActionResult Set(string culture, string redirectUri)
        {
            if (culture != null)
            {
                var requestCulture = new RequestCulture(culture, culture);
                var cookieName = CookieRequestCultureProvider.DefaultCookieName;
                var cookieValue = CookieRequestCultureProvider.MakeCookieValue(requestCulture);

                HttpContext.Response.Cookies.Append(cookieName, cookieValue);
            }

            return LocalRedirect(redirectUri);
        }
    }
}
