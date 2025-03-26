using FastyBoxWeb.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastyBoxWeb.Data.Entities
{
    public class Payment : BaseEntity
    {
        public int Id { get; set; }

        public int ForwardRequestId { get; set; }
        public virtual ForwardRequest? ForwardRequest { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public PaymentType Type { get; set; } = PaymentType.Initial;

        [MaxLength(100)]
        public string? TransactionId { get; set; }

        [MaxLength(250)]
        public string? PaymentMethod { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
