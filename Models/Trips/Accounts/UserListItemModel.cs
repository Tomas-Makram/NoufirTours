namespace NoufirTours.Models.Trips.Accounts
{
    public class UserListItemModel
    {
        public Guid UserID { get; set; }
        public string Username { get; set; } = "";
        public string? FullName { get; set; }
        public string RoleText { get; set; } = "staff";
        public string? Phone { get; set; }
        public bool IsActive { get; set; }

        public decimal TotalDue { get; set; }
        public DateTime? LastLoginCairo { get; set; }
    }
}
