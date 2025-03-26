using FastyBoxWeb.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace FastyBoxWeb.Data.Entities
{
    public class RequestStatusHistory : BaseEntity
    {
        public int Id { get; set; }

        public int ForwardRequestId { get; set; }
        public virtual ForwardRequest? ForwardRequest { get; set; }

        public ForwardRequestStatus Status { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

}
