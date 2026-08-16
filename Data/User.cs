using NoufirTours.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{

    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public Guid UserID { get; set; } = Guid.NewGuid();

        [Required, MaxLength(80)]
        [Column("username")]
        public string Username { get; set; } = default!;

        [Required]
        [Column("pass_hash")]
        public string PasswordHash { get; set; } = default!;

        [Required, MaxLength(20)]
        [Column("role")]
        public string RoleText { get; set; } = "staff";

        [MaxLength(150)]
        [Column("full_name")]
        public string? FullName { get; set; }

        [MaxLength(30)]
        [Column("phone")]
        public string? Phone { get; set; }

        [Column("is_active")]
        public int IsActiveInt { get; set; } = 1;

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        // ------------ Helpers (NotMapped) ------------
        [NotMapped]
        public bool IsActive => IsActiveInt == 1;

        [NotMapped]
        public UserRole Role
            => RoleText?.ToLower() switch
            {
                "admin" => UserRole.Admin,
                "driver" => UserRole.Driver,
                _ => UserRole.Staff
            };

        // ------------ Navigation ------------
        
        public ICollection<Booking> BookingTrips { get; set; } = new List<Booking>();
        public ICollection<Booking> CanceledBookings { get; set; } = new List<Booking>();
        public ICollection<BookingCollection> CollectedPayments { get; set; } = new List<BookingCollection>();
        public ICollection<Trip> DriverTrips { get; set; } = new List<Trip>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}