using FastyBoxWeb.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastyBoxWeb.Data.Entities
{
    public class ForwardRequest : UserOwnedEntity
    {
        public int Id { get; set; }

        [Required]
        public string TrackingCode { get; set; } = string.Empty;

        public ForwardRequestStatus Status { get; set; } = ForwardRequestStatus.Draft;

        [MaxLength(250)]
        public string? Notes { get; set; }

        public int? ShippingAddressId { get; set; }
        public virtual Address? ShippingAddress { get; set; }

        public virtual ICollection<ForwardItem> Items { get; set; } = new HashSet<ForwardItem>();
        public virtual ICollection<Payment> Payments { get; set; } = new HashSet<Payment>();
        public virtual ICollection<RequestStatusHistory> StatusHistory { get; set; } = new HashSet<RequestStatusHistory>();

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPaid => Payments.Where(p => p.Status == PaymentStatus.Succeeded).Sum(p => p.Amount);

        public bool IsPaidInFull => TotalPaid >= FinalTotal && FinalTotal > 0;

        // Información del transportista original
        [MaxLength(100)]
        public string? OriginalCarrier { get; set; }

        [MaxLength(100)]
        public string? OriginalTrackingNumber { get; set; }


        public virtual ICollection<RequiredDocument> RequiredDocuments { get; set; } = new HashSet<RequiredDocument>();
    }
}
