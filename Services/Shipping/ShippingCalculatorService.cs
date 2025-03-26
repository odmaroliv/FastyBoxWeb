using FastyBoxWeb.Data;
using FastyBoxWeb.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FastyBoxWeb.Services.Shipping
{
    public interface IShippingCalculatorService
    {
        Task<decimal> CalculateEstimatedTotalAsync(ForwardRequest request);
        Task<decimal> CalculateShippingCostAsync(ForwardItem item);
        Task<decimal> CalculateCustomsFeesAsync(ForwardItem item);
        Task<ShippingRate> GetApplicableShippingRateAsync(decimal weight);
        Task<CustomsRate> GetApplicableCustomsRateAsync(string category = "General");
    }

    public class ShippingCalculatorService : IShippingCalculatorService
    {
        private readonly ApplicationDbContext _context;

        public ShippingCalculatorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalculateEstimatedTotalAsync(ForwardRequest request)
        {
            // Cargar artículos si aún no se han cargado
            if (request.Items == null || !request.Items.Any())
            {
                var items = await _context.ForwardItems
                    .Where(i => i.ForwardRequestId == request.Id)
                    .ToListAsync();

                if (!items.Any())
                {
                    return 0;
                }

                request.Items = items;
            }

            decimal total = 0;

            // Calcular el costo de envío para cada artículo
            foreach (var item in request.Items)
            {
                var shippingCost = await CalculateShippingCostAsync(item);
                var customsFees = await CalculateCustomsFeesAsync(item);
                total += shippingCost + customsFees;
            }

            return Math.Round(total, 2);
        }

        public async Task<decimal> CalculateShippingCostAsync(ForwardItem item)
        {
            // Si el peso declarado no está disponible, devolver un mínimo estimado
            if (!item.DeclaredWeight.HasValue)
            {
                // Obtener tarifa mínima
                var minRate = await _context.ShippingRates
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.BaseRate)
                    .FirstOrDefaultAsync();

                return minRate?.BaseRate ?? 15.99m; // Valor predeterminado si no hay tarifas
            }

            // Obtener la tarifa aplicable según el peso
            var rate = await GetApplicableShippingRateAsync(item.DeclaredWeight.Value);

            // Calcular el costo base más adicional por kg sobre el mínimo
            var additionalWeight = Math.Max(0, item.DeclaredWeight.Value - rate.MinWeight);
            var additionalCost = additionalWeight * rate.AdditionalPerKg;

            return rate.BaseRate + additionalCost;
        }

        public async Task<decimal> CalculateCustomsFeesAsync(ForwardItem item)
        {
            // Obtener tarifa de aduanas para la categoría del artículo (por defecto General)
            var customsRate = await GetApplicableCustomsRateAsync();

            // Calcular tarifa basada en el valor declarado
            var fee = item.DeclaredValue * customsRate.RatePercentage;

            // Aplicar tarifa mínima si es necesario
            return Math.Max(fee, customsRate.MinimumFee);
        }

        public async Task<ShippingRate> GetApplicableShippingRateAsync(decimal weight)
        {
            // Buscar la tarifa aplicable según el peso
            var rate = await _context.ShippingRates
                .Where(r => r.IsActive && weight >= r.MinWeight && weight <= r.MaxWeight)
                .FirstOrDefaultAsync();

            if (rate == null)
            {
                // Si no hay tarifa exacta, usar la tarifa más alta
                rate = await _context.ShippingRates
                    .Where(r => r.IsActive)
                    .OrderByDescending(r => r.MaxWeight)
                    .FirstOrDefaultAsync();

                if (rate == null)
                {
                    // Fallback a tarifa predeterminada si no hay ninguna configurada
                    rate = new ShippingRate
                    {
                        Name = "Predeterminada",
                        MinWeight = 0,
                        MaxWeight = 100,
                        BaseRate = 39.99m,
                        AdditionalPerKg = 2.5m
                    };
                }
            }

            return rate;
        }

        public async Task<CustomsRate> GetApplicableCustomsRateAsync(string category = "General")
        {
            // Buscar la tarifa de aduanas para la categoría
            var rate = await _context.CustomsRates
                .Where(r => r.IsActive && r.Category == category)
                .FirstOrDefaultAsync();

            if (rate == null)
            {
                // Buscar tarifa general si no hay una específica
                rate = await _context.CustomsRates
                    .Where(r => r.IsActive && r.Category == "General")
                    .FirstOrDefaultAsync();

                if (rate == null)
                {
                    // Fallback a tarifa predeterminada si no hay ninguna configurada
                    rate = new CustomsRate
                    {
                        Name = "Predeterminada",
                        Category = "General",
                        RatePercentage = 0.16m,
                        MinimumFee = 5.0m
                    };
                }
            }

            return rate;
        }
    }
}

