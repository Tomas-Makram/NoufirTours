using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{

    [Table("app_settings")]
    public class AppSetting
    {
        [Key]
        [Column("key")]
        [MaxLength(100)]
        public string Key { get; set; } = default!;

        [Column("value")]
        public string? Value { get; set; }
    }
}