using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastyBoxWeb.Data.Entities
{
    public class CustomsRate : BaseEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal RatePercentage { get; set; } // porcentaje del valor declarado

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumFee { get; set; } // tarifa mínima en USD

        public bool IsActive { get; set; } = true;
    }
}
