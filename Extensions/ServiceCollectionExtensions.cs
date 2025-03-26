using FastyBoxWeb.Services.Notification;
using FastyBoxWeb.Services.Payment;
using FastyBoxWeb.Services.Shipping;
using Microsoft.AspNetCore.Authorization;

namespace FastyBox.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Registrar servicios de aplicación
            services.AddScoped<IForwardingService, ForwardingService>();
            services.AddScoped<INotificationService, EmailNotificationService>();
            services.AddScoped<IPaymentService, StripePaymentService>();
            services.AddScoped<IShippingCalculatorService, ShippingCalculatorService>();

            // Configuración de Stripe
            services.Configure<StripeSettings>(services.BuildServiceProvider()
                .GetRequiredService<IConfiguration>().GetSection("Stripe"));

            return services;
        }

        public static IServiceCollection AddAuthorizationWithPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Política para administradores
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Administrator"));

                // Política para usuarios normales
                options.AddPolicy("RequireUserRole", policy =>
                    policy.RequireRole("User"));

                // Política para acceso a datos propios
                options.AddPolicy("ResourceOwnerOnly", policy =>
                    policy.Requirements.Add(new ResourceOwnerRequirement()));
            });

            // Registrar handler para la política de propietario del recurso
            services.AddScoped<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();

            return services;
        }
    }

    // Requisito de autorización para acceder solo a recursos propios
    public class ResourceOwnerRequirement : IAuthorizationRequirement { }

    // Handler para la política de propietario del recurso
    public class ResourceOwnerAuthorizationHandler : AuthorizationHandler<ResourceOwnerRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ResourceOwnerRequirement requirement)
        {
            if (context.Resource is ResourceOwnerInfo ownerInfo)
            {
                if (context.User.Identity?.Name == ownerInfo.OwnerId)
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }

    // Clase para pasar información del propietario del recurso
    public class ResourceOwnerInfo
    {
        public string OwnerId { get; set; } = string.Empty;
    }
}