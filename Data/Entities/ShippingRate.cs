using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastyBoxWeb.Data.Entities
{
    public class ShippingRate : BaseEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinWeight { get; set; } // en kilogramos

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxWeight { get; set; } // en kilogramos

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseRate { get; set; } // costo base en USD

        [Column(TypeName = "decimal(18,4)")]
        public decimal AdditionalPerKg { get; set; } // costo adicional por kg

        public bool IsActive { get; set; } = true;
    }
}
