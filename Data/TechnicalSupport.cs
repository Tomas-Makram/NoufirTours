using System;
using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Data
{
    public class TechnicalSupport
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(40)]
        public string? CompanyPhone { get; set; }

        [MaxLength(40)]
        public string? ComplaintsPhone { get; set; }

        public long UpdatedAtUnix { get; set; }
        public Guid? UpdatedByUserId { get; set; }

        public bool IsSingleton { get; set; } = true;
    }
}