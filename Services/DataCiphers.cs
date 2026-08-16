using Microsoft.AspNetCore.DataProtection;

namespace NoufirTours.Services
{
    public interface IDataCiphers
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    // [Header] + [IV] + [Encrypted Data] + [Footer]
    public class DataCiphers : IDataCiphers
    {
        private readonly IDataProtector _protector;

        //Create Secret Key To save data
        public DataCiphers(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("EcoRecyclers.UserData");
        }

        //Encryption Data inside database
        public string Encrypt(string plainText)
        {
            return _protector.Protect(plainText);
        }

        //Decryption Data outside database
        public string Decrypt(string cipherText)
        {
            return _protector.Unprotect(cipherText);
        }
    }
}