using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("trip_places")]
    public class TripPlace
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("trip_id")]
        public Guid TripId { get; set; }

        [Required, MaxLength(200)]
        [Column("place_name")]
        public string PlaceName { get; set; } = default!;

        [Column("place_type")]
        public int PlaceTypeInt { get; set; } = (int)TripPlaceType.Stop;

        [NotMapped]
        public TripPlaceType PlaceType => (TripPlaceType)PlaceTypeInt;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        // Location
        [Column("lat")]
        public double? Lat { get; set; }

        [Column("lon")]
        public double? Lon { get; set; }

        // Active/Inactive
        [Column("is_active")]
        public int IsActiveInt { get; set; } = 1;

        [NotMapped]
        public bool IsActive => IsActiveInt == 1;

        // navigation
        [ForeignKey(nameof(TripId))]
        public Trip Trip { get; set; } = default!;
    }
}