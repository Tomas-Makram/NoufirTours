using Microsoft.EntityFrameworkCore;
using NoufirTours.Models;
using NoufirTours.Services;

namespace NoufirTours.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<DBContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<DataHasher>();

            try
            {
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
                _ = await db.Users.AsNoTracking().Select(x => x.UserID).FirstOrDefaultAsync();
            }
            catch
            {
                return;
            }

            var exists = await db.Users.AnyAsync(u =>
                u.Username.ToLower() == "admin" ||
                u.RoleText.ToLower() == "admin");

            if (exists) return;

            var admin = new User
            {
                Username = "admin",
                PasswordHash = hasher.HashData("Admin@12345"),
                RoleText = "admin",
                FullName = "System Administrator",
                IsActiveInt = 1,
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}