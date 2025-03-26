using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Entities;
using FastyBoxWeb.Data.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;

namespace FastyBoxWeb.Services.Notification
{
    public interface INotificationService
    {
        Task SendRequestCreatedNotificationAsync(ForwardRequest request);
        Task SendStatusUpdateNotificationAsync(ForwardRequest request);
        Task SendPaymentConfirmationAsync(Data.Entities.Payment payment);
        Task SendWelcomeEmailAsync(ApplicationUser user);
        Task SendAdminAlertAsync(string subject, string message);
    }

    public class EmailNotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly IConfiguration _configuration;

        public EmailNotificationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<EmailNotificationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendRequestCreatedNotificationAsync(ForwardRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning($"No se pudo enviar notificación: Usuario con ID {request.UserId} no encontrado");
                return;
            }

            var subject = "Solicitud de reenvío creada";
            var body = $@"
                <h2>¡Gracias por crear una solicitud de reenvío!</h2>
                <p>Su código de seguimiento es: <strong>{request.TrackingCode}</strong></p>
                <p>Total estimado: ${request.EstimatedTotal:F2} USD</p>
                <p>Puede seguir el estado de su solicitud en nuestra plataforma.</p>
                <p>Equipo FastyBox</p>
            ";

            await SendEmailAsync(user.Email!, subject, body);

            // Notificar al administrador
            await SendAdminAlertAsync(
                "Nueva solicitud de reenvío",
                $"Se ha creado una nueva solicitud de reenvío con código {request.TrackingCode} por {user.FullName}.");
        }

        public async Task SendStatusUpdateNotificationAsync(ForwardRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning($"No se pudo enviar notificación: Usuario con ID {request.UserId} no encontrado");
                return;
            }

            var statusInfo = GetStatusInfo(request.Status);

            var subject = $"Actualización de estado: {statusInfo.Name}";
            var body = $@"
                <h2>El estado de su solicitud ha cambiado</h2>
                <p>Su solicitud con código <strong>{request.TrackingCode}</strong> ahora está en estado <strong>{statusInfo.Name}</strong>.</p>
                <p>{statusInfo.Description}</p>
                <p>Puede ver más detalles en nuestra plataforma.</p>
                <p>Equipo FastyBox</p>
            ";

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

            var subject = "Confirmación de pago";
            var body = $@"
                <h2>¡Gracias por su pago!</h2>
                <p>Hemos recibido su pago de <strong>${payment.Amount:F2} USD</strong> para la solicitud <strong>{request.TrackingCode}</strong>.</p>
                <p>Tipo de pago: {GetPaymentTypeName(payment.Type)}</p>
                <p>Estado del pago: {GetPaymentStatusName(payment.Status)}</p>
                <p>ID de transacción: {payment.TransactionId}</p>
                <p>Equipo FastyBox</p>
            ";

            await SendEmailAsync(user.Email!, subject, body);
        }

        public async Task SendWelcomeEmailAsync(ApplicationUser user)
        {
            var usWarehouseAddress = await GetUSWarehouseAddressAsync();

            var subject = "¡Bienvenido a FastyBox!";
            var body = $@"
                <h2>¡Bienvenido a FastyBox!</h2>
                <p>Estimado/a {user.FullName},</p>
                <p>Gracias por registrarse en FastyBox, su servicio de reenvío de paquetes desde Estados Unidos a México.</p>
                <p>Su dirección virtual en Estados Unidos es:</p>
                <p style='padding: 10px; border: 1px solid #ddd; background-color: #f9f9f9;'>
                    <strong>{user.FullName}</strong><br/>
                    {usWarehouseAddress}<br/>
                    <strong>ID de Cliente: {user.Id.Substring(0, 8).ToUpper()}</strong>
                </p>
                <p>Use esta dirección cuando realice compras en sitios de Estados Unidos y nosotros nos encargaremos del resto.</p>
                <p>Equipo FastyBox</p>
            ";

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

            var body = $@"
                <h2>Alerta del sistema</h2>
                <p>{message}</p>
                <p>Este es un mensaje automático del sistema FastyBox.</p>
            ";

            foreach (var admin in adminEmails)
            {
                await SendEmailAsync(admin.Email!, $"FastyBox Admin: {subject}", body);
            }
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

            return addressConfig?.Value ?? "123 Shipping St, Miami, FL 33101, USA";
        }

        private (string Name, string Description) GetStatusInfo(ForwardRequestStatus status)
        {
            return status switch
            {
                ForwardRequestStatus.Draft => ("Borrador", "Su solicitud está en estado de borrador."),
                ForwardRequestStatus.AwaitingArrival => ("Esperando Llegada", "Estamos esperando que su paquete llegue a nuestro almacén en EE.UU."),
                ForwardRequestStatus.ReceivedInWarehouse => ("Recibido en Almacén", "¡Buenas noticias! Su paquete ha llegado a nuestro almacén en EE.UU."),
                ForwardRequestStatus.InReview => ("En Revisión", "Estamos revisando su paquete y verificando las dimensiones y peso."),
                ForwardRequestStatus.DocumentsRequired => ("Documentos Requeridos", "Se requieren documentos adicionales para su envío. Por favor revise su cuenta para subir los documentos solicitados."),
                ForwardRequestStatus.AwaitingPayment => ("Esperando Pago", "Por favor complete el pago para continuar con el proceso de envío."),
                ForwardRequestStatus.Processing => ("En Procesamiento", "Su paquete está siendo procesado para envío."),
                ForwardRequestStatus.InTransitToMexico => ("En Tránsito a México", "Su paquete está en camino a México."),
                ForwardRequestStatus.Delivered => ("Entregado", "¡Su paquete ha sido entregado exitosamente!"),
                ForwardRequestStatus.Cancelled => ("Cancelado", "Su solicitud ha sido cancelada."),
                _ => ("Desconocido", "El estado de su solicitud ha cambiado.")
            };
        }

        private string GetPaymentTypeName(PaymentType type)
        {
            return type switch
            {
                PaymentType.Initial => "Pago Inicial",
                PaymentType.Additional => "Pago Adicional",
                PaymentType.Complete => "Pago Completo",
                _ => "Desconocido"
            };
        }

        private string GetPaymentStatusName(PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Pending => "Pendiente",
                PaymentStatus.Processing => "Procesando",
                PaymentStatus.Succeeded => "Completado",
                PaymentStatus.Failed => "Fallido",
                PaymentStatus.Refunded => "Reembolsado",
                _ => "Desconocido"
            };
        }
    }
}

