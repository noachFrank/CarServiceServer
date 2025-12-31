using System.Net;
using System.Net.Mail;

namespace DispatchApp.Server.Services
{
    public class PasswordService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public PasswordService(IConfiguration configuration)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpHost = emailSettings["SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            _smtpUsername = emailSettings["SmtpUsername"] ?? "";
            _smtpPassword = emailSettings["SmtpPassword"] ?? "";
            _fromEmail = emailSettings["FromEmail"] ?? "";
            _fromName = emailSettings["FromName"] ?? "Dispatch App";
        }

        public async Task<bool> SendPasswordEmail(string toEmail, string recipientName, string password, string userType, string userName)
        {
            try
            {
                var subject = "Your Shia's Transportation Dispatcher Account Has Been Created";
                var body = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2>Welcome to Shia's Transportation, {recipientName}!</h2>
    <p>Your {userType} account has been created. Here are your login credentials:</p>
    <div style='background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
        <p><strong>Email:</strong> {toEmail}</p>
        <p><strong>UserName:</strong> {userName}</p>
        <p><strong>Password:</strong> {password}</p>
    </div>
<h3>You can change it after logging in.</h3>

    <p>If you have any questions, please contact your administrator.</p>
    <br/>
    <p>Best regards,<br/>The Shia's Transportation Team</p>
</body>
</html>";

                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                return false;
            }
        }
        public string GeneratePassword(int length = 12)
        {
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%^&*";
            const string allChars = uppercase + lowercase + digits + special;

            var random = new Random();
            var password = new char[length];

            // Ensure at least one of each type
            password[0] = uppercase[random.Next(uppercase.Length)];
            password[1] = lowercase[random.Next(lowercase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            // Fill the rest randomly
            for (int i = 4; i < length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle the password
            return new string(password.OrderBy(x => random.Next()).ToArray());
        }
        public async Task<bool> SendPasswordResetEmail(string toEmail, string newPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(_fromEmail) || string.IsNullOrEmpty(_smtpPassword))
                {
                    Console.WriteLine("⚠️  Email settings not configured. Password: " + newPassword);
                    return false;
                }

                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = "Password Reset - Shia's Transportation",
                    Body = $@"
                        <html>
                        <body>
                            <h2>Password Reset</h2>
                            <p>Your password has been reset.</p>
                            <p><strong>New Password:</strong> {newPassword}</p>
                            <p>If you did not request this password reset, please contact your administrator immediately.</p>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send email: {ex.Message}");
                return false;
            }
        }
    }
}
