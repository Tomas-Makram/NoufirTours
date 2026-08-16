namespace NoufirTours.Models.Trips.Accounts
{
    public class UsersListModel
    {
        public string Q { get; set; } = "";
        public bool OnlyAdmins { get; set; }
        public bool IncludeInactive { get; set; } = true;
        public List<UserListItemModel> Items { get; set; } = new();
    }

}
