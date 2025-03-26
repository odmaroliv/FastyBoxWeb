using System.ComponentModel.DataAnnotations;

namespace FastyBoxWeb.Data.Entities
{
    public class Address : UserOwnedEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string RecipientName { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Street { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? StreetNumber { get; set; }

        [MaxLength(100)]
        public string? Interior { get; set; }

        [Required, MaxLength(100)]
        public string Colony { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Country { get; set; } = "México";

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(250)]
        public string? AdditionalInstructions { get; set; }

        public bool IsDefault { get; set; }

        public string FullAddress => $"{Street} {StreetNumber} {Interior}, {Colony}, {City}, {State}, {PostalCode}";
    }
}
