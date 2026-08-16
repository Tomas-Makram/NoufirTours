using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace NoufirTours.Services
{
    public interface DataHasher
    {
        string HashData(string password);
        bool VerifyHashed(string password, string hashedPassword);
        string HashComparison(string email);
    }

    public class PasswordHasherService : DataHasher
    {
        private readonly PasswordHasher<object> _dataHasher;

        public PasswordHasherService()
        {
            _dataHasher = new PasswordHasher<object>();
        }

        // Hashing Password (Encryption Password in one way)
        public string HashData(string password)
        {
            return _dataHasher.HashPassword(null!, password);
        }

        // Check if Password is Valid or not
        public bool VerifyHashed(string password, string hashedPassword)
        {
            var result = _dataHasher.VerifyHashedPassword(null!, hashedPassword, password);
            return result == PasswordVerificationResult.Success;
        }

        // Create Hashing Data To Can Search and comparison data
        public string HashComparison(string email)
        {
            email = email.Trim().ToLowerInvariant();

            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(email)
            );

            return Convert.ToBase64String(hashBytes);
        }
    }
}