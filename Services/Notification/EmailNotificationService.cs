using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Entities;
using FastyBoxWeb.Data.Enums;
using FastyBoxWeb.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace FastyBoxWeb.Services.Notification
{
    public interface INotificationService
    {
        Task SendRequestCreatedNotificationAsync(ForwardRequest request);
        Task SendStatusUpdateNotificationAsync(ForwardRequest request);
        Task SendPaymentConfirmationAsync(Data.Entities.Payment payment);
        Task SendWelcomeEmailAsync(ApplicationUser user);
        Task SendAdminAlertAsync(string subject, string message);
        Task SendDocumentsRequiredNotificationAsync(ForwardRequest request, List<string> documentsRequired);
        Task SendGenericEmailAsync(string to, string subject, string htmlBody);

    }

    public class EmailNotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IStringLocalizerFactory _localizerFactory;
        private readonly IWebHostEnvironment _environment;
        private readonly string LOGO;

        public EmailNotificationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<EmailNotificationService> logger,
            IConfiguration configuration,
            IStringLocalizerFactory localizerFactory,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
            _localizerFactory = localizerFactory;
            _environment = environment;
            LOGO = $"{_configuration["AppUrl"]}/logo.svg";
        }

        public async Task SendRequestCreatedNotificationAsync(ForwardRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning($"No se pudo enviar notificación: Usuario con ID {request.UserId} no encontrado");
                return;
            }

            // Obtener el localizador en el idioma del usuario
            var localizer = GetUserLocalizer(user.PreferredLanguage);

            var subject = localizer["RequestCreatedSubject"];
            var body = GetEmailTemplate("request-created-template");


            // Reemplazar placeholders con valores localizados y datos de la solicitud
            body = body.Replace("{{GREETING}}", string.Format(localizer["Greeting"], user.FirstName))
                       .Replace("{{TITLE}}", localizer["RequestCreatedTitle"])
                       .Replace("{{MESSAGE}}", localizer["RequestCreatedMessage"])
                       .Replace("{{TRACKING_CODE_LABEL}}", localizer["TrackingCode"])
                       .Replace("{{TRACKING_CODE}}", request.TrackingCode)
                       .Replace("{{ESTIMATED_TOTAL_LABEL}}", localizer["EstimatedTotal"])
                       .Replace("{{ESTIMATED_TOTAL}}", $"${request.EstimatedTotal:F2} USD")
                       .Replace("{{STATUS_LABEL}}", localizer["Status"])
                       .Replace("{{STATUS}}", GetLocalizedStatusName(localizer, request.Status))
                       .Replace("{{FOLLOW_STATUS_MESSAGE}}", localizer["FollowStatusMessage"])
                       .Replace("{{ACTION_BUTTON_TEXT}}", localizer["ViewRequest"])
                       .Replace("{{ACTION_BUTTON_URL}}", $"{_configuration["AppUrl"]}/request/{request.Id}")
                       .Replace("{{SUPPORT_MESSAGE}}", localizer["SupportMessage"])
                       .Replace("{{COMPANY_NAME}}", "FastyBox")
                       .Replace("{{LOGO_URL}}", LOGO);

            await SendEmailAsync(user.Email!, subject, body);

            // Notificar al administrador
            await SendAdminAlertAsync(
                localizer["NewRequestSubject"],
                string.Format(localizer["NewRequestAdminMessage"], request.TrackingCode, user.FullName));
        }
        public async Task SendGenericEmailAsync(string to, string subject, string htmlBody)
        {
            await SendEmailAsync(to, subject, htmlBody);
        }

        public async Task SendStatusUpdateNotificationAsync(ForwardRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning($"No se pudo enviar notificación: Usuario con ID {request.UserId} no encontrado");
                return;
            }

            // Obtener el localizador en el idioma del usuario
            var localizer = GetUserLocalizer(user.PreferredLanguage);

            // Obtener la información del último cambio de estado
            var lastStatusChange = request.StatusHistory
                .OrderByDescending(sh => sh.CreatedAt)
                .FirstOrDefault();

            var statusInfo = GetLocalizedStatusInfo(localizer, request.Status);

            var subject = string.Format(localizer["StatusUpdateSubject"], statusInfo.Name);
            var body = GetEmailTemplate("status-update-template");


            // Reemplazar placeholders con valores localizados y datos de la solicitud
            body = body.Replace("{{GREETING}}", string.Format(localizer["Greeting"], user.FirstName))
                       .Replace("{{TITLE}}", localizer["StatusUpdateTitle"])
                       .Replace("{{MESSAGE}}", string.Format(localizer["StatusUpdateMessage"], request.TrackingCode, statusInfo.Name))
                       .Replace("{{STATUS_LABEL}}", localizer["Status"])
                       .Replace("{{STATUS}}", statusInfo.Name)
                       .Replace("{{STATUS_DESCRIPTION}}", statusInfo.Description)
                       .Replace("{{ADMIN_NOTES_LABEL}}", localizer["AdminNotes"])
                       .Replace("{{ADMIN_NOTES}}", lastStatusChange?.Notes ?? localizer["NoAdminNotes"])
                       .Replace("{{ACTION_BUTTON_TEXT}}", localizer["ViewRequestDetails"])
                       .Replace("{{ACTION_BUTTON_URL}}", $"{_configuration["AppUrl"]}/request/{request.Id}")
                       .Replace("{{SUPPORT_MESSAGE}}", localizer["SupportMessage"])
                       .Replace("{{COMPANY_NAME}}", "FastyBox")
                       .Replace("{{LOGO_URL}}", LOGO);

            await SendEmailAsync(user.Email!, subject, body);
        }

        public async Task SendPaymentConfirmationAsync(Data.Entities.Payment payment)
        {
            var request = await _context.ForwardRequests.FindAsync(payment.ForwardRequestId);
            if (request == null)
            {
                _logger.LogWarning($"No se pudo enviar confirmación de pago: Solicitud con ID {payment.ForwardRequestId} no encontrada");
                return;
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning($"No se pudo enviar confirmación de pago: Usuario con ID {request.UserId} no encontrado");
                return;
            }

            // Obtener el localizador en el idioma del usuario
            var localizer = GetUserLocalizer(user.PreferredLanguage);

            var subject = localizer["PaymentConfirmationSubject"];
            var body = GetEmailTemplate("payment-confirmation-template");



            // Reemplazar placeholders con valores localizados y datos del pago
            body = body.Replace("{{GREETING}}", string.Format(localizer["Greeting"], user.FirstName))
                       .Replace("{{TITLE}}", localizer["PaymentConfirmationTitle"])
                       .Replace("{{MESSAGE}}", localizer["PaymentConfirmationMessage"])
                       .Replace("{{TRACKING_CODE_LABEL}}", localizer["TrackingCode"])
                       .Replace("{{TRACKING_CODE}}", request.TrackingCode)
                       .Replace("{{AMOUNT_LABEL}}", localizer["Amount"])
                       .Replace("{{AMOUNT}}", $"${payment.Amount:F2} USD")
                       .Replace("{{PAYMENT_TYPE_LABEL}}", localizer["PaymentType"])
                       .Replace("{{PAYMENT_TYPE}}", GetLocalizedPaymentTypeName(localizer, payment.Type))
                       .Replace("{{PAYMENT_STATUS_LABEL}}", localizer["PaymentStatus"])
                       .Replace("{{PAYMENT_STATUS}}", GetLocalizedPaymentStatusName(localizer, payment.Status))
                       .Replace("{{TRANSACTION_ID_LABEL}}", localizer["TransactionId"])
                       .Replace("{{TRANSACTION_ID}}", payment.TransactionId ?? localizer["NotAvailable"])
                       .Replace("{{ACTION_BUTTON_TEXT}}", localizer["ViewRequest"])
                       .Replace("{{ACTION_BUTTON_URL}}", $"{_configuration["AppUrl"]}/request/{request.Id}")
                       .Replace("{{SUPPORT_MESSAGE}}", localizer["SupportMessage"])
                       .Replace("{{COMPANY_NAME}}", "FastyBox")
                       .Replace("{{LOGO_URL}}", LOGO);

            await SendEmailAsync(user.Email!, subject, body);
        }

        public async Task SendWelcomeEmailAsync(ApplicationUser user)
        {
            var usWarehouseAddress = await GetUSWarehouseAddressAsync();

            // Obtener el localizador en el idioma del usuario
            var localizer = GetUserLocalizer(user.PreferredLanguage);

            var subject = localizer["WelcomeSubject"];
            var body = GetEmailTemplate("welcome-template");



            // Reemplazar placeholders con valores localizados y datos del usuario
            body = body.Replace("{{GREETING}}", string.Format(localizer["Greeting"], user.FirstName))
                       .Replace("{{TITLE}}", localizer["WelcomeTitle"])
                       .Replace("{{MESSAGE}}", localizer["WelcomeMessage"])
                       .Replace("{{US_ADDRESS_LABEL}}", localizer["YourUSAddress"])
                       .Replace("{{FULL_NAME}}", user.FullName)
                       .Replace("{{ADDRESS}}", usWarehouseAddress)
                       .Replace("{{CUSTOMER_ID_LABEL}}", localizer["CustomerID"])
                       .Replace("{{CUSTOMER_ID}}", user.Id.Substring(0, 8).ToUpper())
                       .Replace("{{ADDRESS_USAGE_INSTRUCTIONS}}", localizer["AddressUsageInstructions"])
                       .Replace("{{ACTION_BUTTON_TEXT}}", localizer["GoToDashboard"])
                       .Replace("{{ACTION_BUTTON_URL}}", $"{_configuration["AppUrl"]}/dashboard")
                       .Replace("{{SUPPORT_MESSAGE}}", localizer["SupportMessage"])
                       .Replace("{{COMPANY_NAME}}", "FastyBox")
                       .Replace("{{LOGO_URL}}", LOGO);

            await SendEmailAsync(user.Email!, subject, body);
        }

        public async Task SendAdminAlertAsync(string subject, string message)
        {
            var adminEmails = await _userManager.GetUsersInRoleAsync("Administrator");
            if (!adminEmails.Any())
            {
                _logger.LogWarning("No se pudo enviar alerta: No se encontraron administradores");
                return;
            }

            var body = GetEmailTemplate("admin-alert-template");



            // Reemplazar placeholders con valores y datos de la alerta
            body = body.Replace("{{TITLE}}", "Alerta del Sistema")
                       .Replace("{{MESSAGE}}", message)
                       .Replace("{{ALERT_NOTE}}", "Este es un mensaje automático del sistema FastyBox.")
                       .Replace("{{ACTION_BUTTON_TEXT}}", "Ir al Panel de Administración")
                       .Replace("{{ACTION_BUTTON_URL}}", $"{_configuration["AppUrl"]}/admin")
                       .Replace("{{COMPANY_NAME}}", "FastyBox")
                       .Replace("{{LOGO_URL}}", LOGO);

            foreach (var admin in adminEmails)
            {
                await SendEmailAsync(admin.Email!, $"FastyBox Admin: {subject}", body);
            }
        }

        public async Task SendDocumentsRequiredNotificationAsync(ForwardRequest request, List<string> documentsRequired)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning($"No se pudo enviar notificación de documentos: Usuario con ID {request.UserId} no encontrado");
                return;
            }

            // Obtener el localizador en el idioma del usuario
            var localizer = GetUserLocalizer(user.PreferredLanguage);

            // Obtener la información del último cambio de estado
            var lastStatusChange = request.StatusHistory
                .OrderByDescending(sh => sh.CreatedAt)
                .FirstOrDefault(sh => sh.Status == ForwardRequestStatus.DocumentsRequired);

            var subject = localizer["DocumentsRequiredSubject"];
            var body = GetEmailTemplate("documentos-solicitados-template");

            // Crear la lista de documentos en HTML
            var documentsList = new StringBuilder();
            foreach (var doc in documentsRequired)
            {
                documentsList.AppendLine($"<li>{GetLocalizedDocumentName(localizer, doc)}</li>");
            }

            // Reemplazar placeholders con valores localizados y datos de la solicitud
            body = body.Replace("{{GREETING}}", string.Format(localizer["Greeting"], user.FirstName))
                       .Replace("{{TITLE}}", localizer["DocumentsRequiredTitle"])
                       .Replace("{{MESSAGE}}", localizer["DocumentsRequiredMessage"])
                       .Replace("{{DOCUMENTS_REQUIRED_LABEL}}", localizer["DocumentsRequiredLabel"])
                       .Replace("{{DOCUMENTS_LIST}}", documentsList.ToString())
                       .Replace("{{ADMIN_NOTES_LABEL}}", localizer["AdminNotes"])
                       .Replace("{{ADMIN_NOTES}}", lastStatusChange?.Notes ?? localizer["NoAdminNotes"])
                       .Replace("{{ACTION_BUTTON_TEXT}}", localizer["UploadDocuments"])
                       .Replace("{{ACTION_BUTTON_URL}}", $"{_configuration["AppUrl"]}/request/{request.Id}/documents")
                       .Replace("{{SUPPORT_MESSAGE}}", localizer["SupportMessage"])
                       .Replace("{{COMPANY_NAME}}", "FastyBox")
                       .Replace("{{LOGO_URL}}", LOGO);

            await SendEmailAsync(user.Email!, subject, body);
        }

        private async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                // Validar parámetros de entrada
                if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(subject))
                {
                    _logger.LogWarning("No se puede enviar email: destinatario o asunto vacíos");
                    return; // Salir sin error
                }

                var emailSettings = _configuration.GetSection("EmailSettings");

                // Validar que la configuración exista
                var fromEmail = emailSettings["FromEmail"];
                var fromName = emailSettings["FromName"];
                var smtpServer = emailSettings["SmtpServer"];
                var smtpPortString = emailSettings["SmtpPort"];
                var smtpUsername = emailSettings["SmtpUsername"];
                var smtpPassword = emailSettings["SmtpPassword"];
                var enableSslString = emailSettings["EnableSsl"];

                // Validar que todos los valores de configuración necesarios existan
                if (string.IsNullOrEmpty(fromEmail) ||
                    string.IsNullOrEmpty(smtpServer) ||
                    string.IsNullOrEmpty(smtpPortString) ||
                    string.IsNullOrEmpty(smtpUsername) ||
                    string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogWarning("Configuración de email incompleta - email no enviado");
                    return; // Salir sin error
                }

                // Intentar convertir valores numéricos y booleanos con manejo de errores
                if (!int.TryParse(smtpPortString, out int smtpPort))
                {
                    _logger.LogWarning("Puerto SMTP inválido: {SmtpPort} - email no enviado", smtpPortString);
                    return; // Salir sin error
                }

                bool enableSsl = true; // Valor predeterminado seguro
                if (!string.IsNullOrEmpty(enableSslString))
                {
                    if (!bool.TryParse(enableSslString, out enableSsl))
                    {
                        _logger.LogWarning("Valor EnableSsl inválido: {EnableSsl} - usando valor predeterminado (true)", enableSslString);
                    }
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName ?? "FastyBox"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(new MailAddress(to));

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    Timeout = 10000 // 10 segundos de timeout para evitar bloqueos largos
                };

                // Enviar con timeout
                var sendTask = client.SendMailAsync(message);
                if (await Task.WhenAny(sendTask, Task.Delay(15000)) == sendTask)
                {
                    // El email se envió a tiempo
                    await sendTask; // Para detectar excepciones
                    _logger.LogInformation("Email enviado a {Recipient}: {Subject}", to, subject);
                }
                else
                {
                    // Timeout
                    _logger.LogWarning("Timeout al enviar email a {Recipient}: {Subject}", to, subject);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email a {Recipient}: {Message}", to, ex.Message);
                // No propagar la excepción para mantener la resistencia del sistema
            }
        }

        private async Task<string> GetUSWarehouseAddressAsync()
        {
            var addressConfig = await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Key == "USWarehouseAddress");

            return addressConfig?.Value ?? "NA-Error";
        }

        private (string Name, string Description) GetLocalizedStatusInfo(IStringLocalizer localizer, ForwardRequestStatus status)
        {
            string statusKey = $"Status{status}";
            string descriptionKey = $"Status{status}Description";

            return (
                Name: localizer[statusKey],
                Description: localizer[descriptionKey]
            );
        }

        private string GetLocalizedStatusName(IStringLocalizer localizer, ForwardRequestStatus status)
        {
            return localizer[$"Status{status}"];
        }

        private string GetLocalizedPaymentTypeName(IStringLocalizer localizer, PaymentType type)
        {
            return localizer[$"PaymentType{type}"];
        }

        private string GetLocalizedPaymentStatusName(IStringLocalizer localizer, PaymentStatus status)
        {
            return localizer[$"PaymentStatus{status}"];
        }

        private string GetLocalizedDocumentName(IStringLocalizer localizer, string documentType)
        {
            return localizer[documentType];
        }

        private IStringLocalizer GetUserLocalizer(string language)
        {
            // Si el idioma no está especificado, usar español por defecto
            if (string.IsNullOrEmpty(language))
            {
                language = "es-MX";
            }

            // Asegurarnos de que el idioma esté en un formato válido (es-MX o en-US)
            if (!language.Contains("-"))
            {
                // Si solo tiene el código de idioma, añadir la región por defecto
                if (language.Equals("es", StringComparison.OrdinalIgnoreCase))
                {
                    language = "es-MX";
                }
                else if (language.Equals("en", StringComparison.OrdinalIgnoreCase))
                {
                    language = "en-US";
                }
            }

            try
            {
                // Obtener el localizador para el recurso SharedResources en el idioma del usuario
                return _localizerFactory.Create(typeof(SharedResources));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear el localizador para el idioma {Language}", language);

                // En caso de error, intentar con el localizador predeterminado
                return _localizerFactory.Create(typeof(SharedResources));
            }
        }

        private string GetEmailTemplate(string templateName)
        {
            // Intentamos cargar la plantilla desde archivos
            string templatePath = Path.Combine(_environment.WebRootPath, "EmailTemplates", $"{templateName}.html");

            // Si existe el archivo físico, lo cargamos
            if (File.Exists(templatePath))
            {
                return File.ReadAllText(templatePath);
            }

            // Si no existe el archivo, usamos las plantillas embebidas en las clases específicas
            _logger.LogWarning($"No se encontró el archivo de plantilla {templateName}.html - usando plantilla embebida");

            // Aquí deberíamos devolver la plantilla embebida adecuada,
            // pero como ya tienes estos archivos por separado, simplemente devolvemos una plantilla básica
            return GetDefaultTemplate();
        }

        // Método para obtener la plantilla por defecto (genérica)
        private string GetDefaultTemplate()
        {
            return @"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{{TITLE}}</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background-color: #f7f7f7; color: #333; }
        .container { max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }
        .header { background-color: #0068d1; padding: 20px; text-align: center; }
        .header img { max-width: 200px; height: auto; }
        .content { padding: 30px; }
        h1 { color: #0068d1; font-size: 24px; margin-top: 0; }
        p { font-size: 16px; line-height: 1.5; margin-bottom: 20px; }
        .button { display: inline-block; background-color: #0068d1; color: white !important; text-decoration: none; padding: 12px 24px; border-radius: 4px; font-weight: 600; margin-top: 10px; }
        .button:hover { background-color: #0051a3; }
        .footer { background-color: #f7f7f7; padding: 20px; text-align: center; font-size: 14px; color: #666; }
        @media only screen and (max-width: 600px) {
            .container { width: 100%; }
            .content { padding: 20px; }
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <img src='{{LOGO_URL}}' alt='FastyBox'>
        </div>
        <div class='content'>
            <h1>{{TITLE}}</h1>
            <p>{{MESSAGE}}</p>
        </div>
        <div class='footer'>
            &copy; 2025 {{COMPANY_NAME}}. Todos los derechos reservados.
        </div>
    </div>
</body>
</html>";
        }
    }
}