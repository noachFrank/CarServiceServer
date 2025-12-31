namespace DispatchApp.Server.Data.DataTypes
{
    public class UpdatePasswordRequest
    {
        public int UserId { get; set; }
        public string? UserType { get; set; }
        public string? OldPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public int UserId { get; set; }
        public string? UserType { get; set; }
    }
}
