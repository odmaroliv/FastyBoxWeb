using System.ComponentModel.DataAnnotations;

namespace FastyBoxWeb.Data.Entities
{
    public class SystemConfiguration : BaseEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }
    }
}
