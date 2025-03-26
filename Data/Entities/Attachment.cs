using System.ComponentModel.DataAnnotations;

namespace FastyBoxWeb.Data.Entities
{
    public class Attachment : BaseEntity
    {
        public int Id { get; set; }

        public int ForwardItemId { get; set; }
        public virtual ForwardItem? ForwardItem { get; set; }

        [Required, MaxLength(200)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public long FileSize { get; set; }
    }
}
