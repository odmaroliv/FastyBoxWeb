using FastyBoxWeb.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Encodings.Web;

namespace FastyBoxWeb.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class UnconfirmedAccountModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public UnconfirmedAccountModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public bool EmailSent { get; set; }

        public async Task<IActionResult> OnGetAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound($"No se pudo cargar el usuario con email '{email}'.");
            }

            Email = email;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Email))
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                return NotFound($"No se pudo cargar el usuario con email '{Email}'.");
            }

            if (user.EmailConfirmed)
            {
                StatusMessage = "Tu cuenta ya ha sido confirmada. Por favor inicia sesión.";
                return RedirectToPage("./Login");
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, code = code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                Email,
                "Confirma tu correo electrónico",
                $"Por favor confirma tu cuenta haciendo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clic aquí</a>.");

            EmailSent = true;
            StatusMessage = "Correo de verificación enviado. Por favor revisa tu email.";

            return Page();
        }
    }
}