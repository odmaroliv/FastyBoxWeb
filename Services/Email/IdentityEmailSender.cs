using FastyBoxWeb.Services.Notification;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace FastyBoxWeb.Services.Email
{
    public class IdentityEmailSender : IEmailSender
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<IdentityEmailSender> _logger;

        public IdentityEmailSender(INotificationService notificationService, ILogger<IdentityEmailSender> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {

                // usaremos SendAdminAlertAsync como alternativa temporal
                await _notificationService.SendGenericEmailAsync(email, subject, htmlMessage);

                // Registrar que se envió el correo
                _logger.LogInformation("Correo de confirmación enviado a {Email}: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de confirmación a {Email}", email);
            }
        }
    }
}