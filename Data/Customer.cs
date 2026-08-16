using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("customers")]
    public class Customer
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(120)]
        [Column("full_name")]
        public string FullName { get; set; } = "";

        [Required, MaxLength(30)]
        [Column("phone")]
        public string Phone { get; set; } = "";

        [Column("created_at_unix")]
        public long CreatedAtUnix { get; set; }
    }
}