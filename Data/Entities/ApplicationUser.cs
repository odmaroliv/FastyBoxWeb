using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FastyBoxWeb.Data.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? PreferredLanguage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? PreferredTheme { get; set; }
        public string FullName => $"{FirstName} {LastName}";

        public virtual ICollection<Address> Addresses { get; set; } = new HashSet<Address>();
        public virtual ICollection<ForwardRequest> ForwardRequests { get; set; } = new HashSet<ForwardRequest>();
    }
}
