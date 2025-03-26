using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Mail;

namespace FastyBoxWeb.Data.Entities
{
    public class ForwardItem : BaseEntity
    {
        public int Id { get; set; }

        public int ForwardRequestId { get; set; }
        public virtual ForwardRequest? ForwardRequest { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Url { get; set; }

        [MaxLength(150)]
        public string? Vendor { get; set; }

        // Dimensiones declaradas
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeclaredWeight { get; set; } // en kilogramos

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeclaredLength { get; set; } // en centímetros

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeclaredWidth { get; set; } // en centímetros

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeclaredHeight { get; set; } // en centímetros

        // Dimensiones reales (verificadas)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualWeight { get; set; } // en kilogramos

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualLength { get; set; } // en centímetros

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualWidth { get; set; } // en centímetros

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualHeight { get; set; } // en centímetros

        // Valor declarado
        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal DeclaredValue { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Para referenciar las facturas o fotos adjuntas
        public virtual ICollection<Attachment> Attachments { get; set; } = new HashSet<Attachment>();
    }
}
