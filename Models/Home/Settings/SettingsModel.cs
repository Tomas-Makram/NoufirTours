using NoufirTours.Models.Trips.Accounts;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Home.Settings
{
    public class SettingsModel
    {
        // UI messages
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        // Tab
        public string ActiveTab { get; set; } = "profile";

        // User info
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public string RoleText { get; set; } = "staff";
        public bool IsActive { get; set; }

        // Profile
        [MaxLength(150)]
        public string? FullName { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        // Password
        public ChangePasswordModel Password { get; set; } = new ChangePasswordModel();

        public List<BookingFinanceRowModel> FinanceBookings { get; set; } = new();
        public List<CollectionRowModel> RecentCollections { get; set; } = new();

        // Audit (Search fields)
        public string? AuditFrom { get; set; }   // yyyy-MM-dd
        public string? AuditTo { get; set; }     // yyyy-MM-dd
        public string? AuditAction { get; set; } // text

        // Audit Logs result
        public List<AuditLogRowModel> AuditLogs { get; set; } = new();
    }
}