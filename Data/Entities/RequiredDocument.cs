using System.ComponentModel.DataAnnotations;

namespace FastyBoxWeb.Data.Entities
{
    public class RequiredDocument : BaseEntity
    {
        public int Id { get; set; }

        public int ForwardRequestId { get; set; }
        public virtual ForwardRequest? ForwardRequest { get; set; }

        [Required, MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsSubmitted { get; set; } = false;

        // Si el documento ya fue enviado, relación con el adjunto
        public int? AttachmentId { get; set; }
        public virtual Attachment? Attachment { get; set; }
    }
}
