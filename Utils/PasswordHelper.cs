namespace DispatchApp.Server.Utils
{

        using BCrypt.Net;

public class PasswordHelper
    {
        // Hash a password
        public static string HashPassword(string password)
        {
            return BCrypt.HashPassword(password);
        }

        // Verify password
        public static bool VerifyPassword(string password, string storedHash)
        {
            return BCrypt.Verify(password, storedHash);
        }
    }
}
