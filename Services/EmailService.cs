using System.Net;
using System.Net.Mail;

namespace DispatchApp.Server.Services
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpHost = emailSettings["SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            _smtpUsername = emailSettings["SmtpUsername"] ?? "";
            _smtpPassword = emailSettings["SmtpPassword"] ?? "";
            _fromEmail = emailSettings["FromEmail"] ?? "";
            _fromName = emailSettings["FromName"] ?? "Dispatch App";
        }

        public EmailService(string smtpUsername, string smtpPassword, string fromEmail, string? fromName = null)
        {
            _smtpUsername = smtpUsername;
            _smtpPassword = smtpPassword;
            _fromEmail = fromEmail;
            _fromName = fromName ?? "Shia's Transportation";
        }

        public async Task<bool> SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            List<(string FileName, byte[] Content, string MimeType)>? attachments = null)
        {
            try
            {
                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        var stream = new MemoryStream(attachment.Content);

                        var mailAttachment = new Attachment(
                            stream,
                            attachment.FileName,
                            attachment.MimeType
                        );

                        mailMessage.Attachments.Add(mailAttachment);
                    }
                }

                await client.SendMailAsync(mailMessage);

                Console.WriteLine($"✅ Email sent successfully to {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send email to {toEmail}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendDriverInvoiceAsync(
            string driverEmail,
            string driverName,
            byte[] pdfInvoice,
            string invoiceNumber,
            decimal netAmount,
            bool driverOwesCompany,
            DateTime fromDate,
            DateTime toDate)
        {
            try
            {
                var subject = $"Invoice #{invoiceNumber} for {fromDate.ToString("MMM d, yyyy")} to {toDate.ToString("MMM d, yyyy")} - Shia's Transportation";

                var amountText = driverOwesCompany
                    ? $"Amount you paid: <strong style=\"color: #d32f2f;\">${Math.Abs(netAmount):F2}</strong>"
                    : $"Amount paid to you: <strong style=\"color: #2e7d32;\">${netAmount:F2}</strong>";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .header {{ background-color: #1976d2; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; }}
        .amount-box {{ background-color: #f5f5f5; padding: 15px; border-radius: 8px; margin: 20px 0; }}
        .footer {{ background-color: #f5f5f5; padding: 15px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🚗 Shia's Transportation</h1>
        <h2>Invoice for {fromDate.Date} to {toDate.Date}</h2>
    </div>
    <div class=""content"">
        <p>Dear {driverName},</p>
        <p>Please find attached your invoice for completed rides.</p>
        
        <div class=""amount-box"">
            <h3>Invoice #{invoiceNumber}</h3>
            <p>{amountText}</p>
        </div>
        
        <p>The attached PDF contains a detailed breakdown of all rides that have been settled up.</p>
        
        <p>If you have any questions about this invoice, please contact dispatch.</p>
        
        <p>Thank you for driving with us!</p>
    </div>
    <div class=""footer"">
        <p>Shia's Transportation | This is an automated message</p>
    </div>
</body>
</html>";

                var attachments = new List<(string FileName, byte[] Content, string MimeType)>
            {
                ($"Invoice_{invoiceNumber}.pdf", pdfInvoice, "application/pdf")
            };

                return await SendEmailAsync(driverEmail, subject, body, attachments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send email : {ex.Message}");
                return false;
            }
        }


        public async Task<bool> SendPasswordResetEmail(string toEmail, string newPassword)
        {
            try
            {
                var subject = "Password Reset - Shia's Transportation";
                var body = $@"
                        <html>
                        <body>
                            <h2>Password Reset</h2>
                            <p>Your password has been reset.</p>
                            <p><strong>New Password:</strong> {newPassword}</p>
                            <p>If you did not request this password reset, please contact your administrator immediately.</p>
                        </body>
                        </html>
                    ";


                SendEmailAsync(toEmail, subject, body);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send email: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendPasswordEmail(string toEmail, string recipientName, string password, string userType, string userName)
        {
            try
            {
                var subject = $"Your Shia's Transportation {(userType.ToLower() == "dispatcher" ? "Dispatcher" : "Driver")} Account Has Been Created";

                // Different content based on user type
                string appAccessSection;
                if (userType.ToLower() == "dispatcher")
                {
                    appAccessSection = $@"
    <div style='background-color: #e8f4fd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #2196F3;'>
        <h3 style='margin-top: 0; color: #1976D2;'>📱 Access the Dispatch App</h3>
        <p>Login to the dispatcher dashboard at:</p>
        <p><a href='https://www.shiastransportation.com/login' style='color: #1976D2; font-size: 16px;'>https://www.shiastransportation.com/login</a></p>
        <p style='margin-bottom: 0;'><strong>💡 Tip:</strong> Bookmark this page so you can easily find it again!</p>
    </div>";
                }
                else
                {
                    appAccessSection = $@"
    <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #4CAF50;'>
        <h3 style='margin-top: 0; color: #388E3C;'>📱 Download the Driver App</h3>
        <p>Get the Shia's Transportation Driver app on your phone:</p>
        <p>
            <a href='https://play.google.com/store/apps/details?id=com.shiastransportation.driver' style='display: inline-block; background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; margin-right: 10px;'>
                ▶ Google Play Store
            </a>
        </p>
        <p>
            <a href='https://apps.apple.com/app/shias-transportation-driver' style='display: inline-block; background-color: #333; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                🍎 Apple App Store
            </a>
        </p>
    </div>";
                }

                var body = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2>Welcome to Shia's Transportation, {recipientName}!</h2>
    <p>Your {userType} account has been created. Here are your login credentials:</p>
    <div style='background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
        <p><strong>Email:</strong> {toEmail}</p>
        <p><strong>Username:</strong> {userName}</p>
        <p><strong>Temporary Password:</strong> {password}</p>
    </div>
    
    <p>⚠️ <strong>Please change your password after your first login.</strong></p>

    {appAccessSection}

    <p>If you have any questions, please contact your administrator.</p>
    <br/>
    <p>Best regards,<br/>The Shia's Transportation Team</p>
</body>
</html>";

                await SendEmailAsync(toEmail, subject, body);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                return false;
            }
        }

    }
}
