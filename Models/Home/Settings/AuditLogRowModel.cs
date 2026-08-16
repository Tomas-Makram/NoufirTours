namespace NoufirTours.Models.Home.Settings
{
    public class AuditLogRowModel
    {
        public long CreatedAtUnix { get; set; }
        public string CreatedAtText { get; set; } = "";

        public string Action { get; set; } = "";
        public string Entity { get; set; } = "";
    }
}
