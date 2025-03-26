using FastyBoxWeb.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace FastyBoxWeb.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // Crear roles si no existen
            await CreateRolesAsync(roleManager);

            // Crear usuario administrador si no existe
            await CreateAdminUserAsync(userManager);

            // Configuraciones del sistema
            await CreateSystemConfigurationsAsync(context);

            // Tarifas de envío
            await CreateShippingRatesAsync(context);

            // Tarifas de aduanas
            await CreateCustomsRatesAsync(context);

            await context.SaveChangesAsync();
        }

        private static async Task CreateRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Administrator", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task CreateAdminUserAsync(UserManager<ApplicationUser> userManager)
        {
            var adminEmail = "admin@fastybox.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    EmailConfirmed = true,
                    PreferredLanguage = "es"
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                }
            }
        }

        private static async Task CreateSystemConfigurationsAsync(ApplicationDbContext context)
        {
            if (!context.SystemConfigurations.Any())
            {
                var configs = new List<SystemConfiguration>
                {
                    new SystemConfiguration
                    {
                        Key = "USWarehouseAddress",
                        Value = "123 Shipping St, Miami, FL 33101, USA",
                        Description = "Dirección del almacén en Estados Unidos"
                    },
                    new SystemConfiguration
                    {
                        Key = "DefaultShippingRate",
                        Value = "1",
                        Description = "ID de la tarifa de envío predeterminada"
                    },
                    new SystemConfiguration
                    {
                        Key = "DefaultCustomsRate",
                        Value = "1",
                        Description = "ID de la tarifa de aduanas predeterminada"
                    }
                };

                await context.SystemConfigurations.AddRangeAsync(configs);
            }
        }

        private static async Task CreateShippingRatesAsync(ApplicationDbContext context)
        {
            if (!context.ShippingRates.Any())
            {
                var rates = new List<ShippingRate>
                {
                    new ShippingRate
                    {
                        Name = "Ligero",
                        MinWeight = 0,
                        MaxWeight = 2,
                        BaseRate = 15.99m,
                        AdditionalPerKg = 0,
                        IsActive = true
                    },
                    new ShippingRate
                    {
                        Name = "Medio",
                        MinWeight = 2.01m,
                        MaxWeight = 5,
                        BaseRate = 25.99m,
                        AdditionalPerKg = 2.5m,
                        IsActive = true
                    },
                    new ShippingRate
                    {
                        Name = "Pesado",
                        MinWeight = 5.01m,
                        MaxWeight = 10,
                        BaseRate = 39.99m,
                        AdditionalPerKg = 3.75m,
                        IsActive = true
                    },
                    new ShippingRate
                    {
                        Name = "Extra pesado",
                        MinWeight = 10.01m,
                        MaxWeight = 50,
                        BaseRate = 59.99m,
                        AdditionalPerKg = 5.0m,
                        IsActive = true
                    }
                };

                await context.ShippingRates.AddRangeAsync(rates);
            }
        }

        private static async Task CreateCustomsRatesAsync(ApplicationDbContext context)
        {
            if (!context.CustomsRates.Any())
            {
                var rates = new List<CustomsRate>
                {
                    new CustomsRate
                    {
                        Name = "Estándar",
                        Category = "General",
                        RatePercentage = 0.16m, // 16%
                        MinimumFee = 5.0m,
                        IsActive = true
                    },
                    new CustomsRate
                    {
                        Name = "Medicamentos",
                        Category = "Salud",
                        RatePercentage = 0.08m, // 8%
                        MinimumFee = 2.5m,
                        IsActive = true
                    },
                    new CustomsRate
                    {
                        Name = "Electrónicos",
                        Category = "Tecnología",
                        RatePercentage = 0.19m, // 19%
                        MinimumFee = 10.0m,
                        IsActive = true
                    }
                };

                await context.CustomsRates.AddRangeAsync(rates);
            }
        }
    }
}
