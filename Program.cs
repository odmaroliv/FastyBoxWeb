using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Entities;
using FastyBoxWeb.Services;
using FastyBoxWeb.Services.Email;
using FastyBoxWeb.Services.Notification;
using FastyBoxWeb.Services.Payment;
using FastyBoxWeb.Services.Shipping;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);


// Configuración de localization
builder.Services.AddLocalization();

// Configuración de servicios
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();

        // Aumentar estos valores para mayor estabilidad
        options.DisconnectedCircuitMaxRetained = 200;  // Aumentado de 100
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);  // Aumentado de 5

        // Tiempo de espera para operaciones JavaScript
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(10);  // Aumentado de 5

        // Este es crítico - aumentar el buffer para evitar problemas de conexión
        options.MaxBufferedUnacknowledgedRenderBatches = 50;  // Aumentado de 20
    });

builder.Services.AddSignalR(options =>
{
    // Aumentar significativamente para operaciones de archivos grandes
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(15);  // Aumentado de 5

    // Reducir el intervalo de keep-alive para detectar desconexiones más rápido
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);  // Reducido de 30

    // Aumentar el tamaño máximo de mensaje considerablemente
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024;  // Aumentado a 100 MB

    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Configuración de la base de datos
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

});

//builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();


builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Configura las cookies INMEDIATAMENTE después de AddIdentity
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    // Handle successful login redirects
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                ctx.Response.Redirect(ctx.RedirectUri);
            }
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            }
            else
            {
                ctx.Response.Redirect(ctx.RedirectUri);
            }
            return Task.CompletedTask;
        },
        OnSignedIn = ctx =>
        {
            ctx.Response.Redirect("/dashboard");
            return Task.CompletedTask;
        }
    };
});

// Configuración de la autenticación para Blazor Server
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();






// Regístrar servicios de la aplicación
builder.Services.AddScoped<IForwardingService, ForwardingService>();
builder.Services.AddScoped<IShippingCalculatorService, ShippingCalculatorService>();
builder.Services.AddScoped<INotificationService, EmailNotificationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentService, StripePaymentService>();
// Registrar IEmailSender para la confirmación de email de Identity
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSender>();

var uploadsDirectory = Path.Combine(builder.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsDirectory))
{
    Directory.CreateDirectory(uploadsDirectory);
}

// Configurar opciones de Stripe
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentService, StripePaymentService>();

// Configuración de HttpContextAccessor (necesario para algunos servicios)
builder.Services.AddHttpContextAccessor();

// Registrar HttpClient para servicios externos
builder.Services.AddHttpClient();

// Configuración de autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministratorRole", policy =>
        policy.RequireRole("Administrator"));

    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
});

var app = builder.Build();
string[] supportedCultures = ["en-US", "es-MX"];
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);


// Configuración del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
    RequestPath = ""
});


app.UseStaticFiles();


app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        // Solo bloqueamos acceso directo a uploads, no a otros archivos estáticos
        context.Response.StatusCode = 403;
        return;
    }

    await next();
});

app.UseRequestLocalization(localizationOptions);
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Aplicar migraciones y sembrar datos iniciales
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();

        // Seed de datos iniciales
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedData.InitializeAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();