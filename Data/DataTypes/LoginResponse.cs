namespace DispatchApp.Server.Data.DataTypes
{
    public class LoginResponse
    {
        public string Token { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string UserType { get; set; }
        public bool IsAdmin { get; set; }

        // Include user details for convenience
        public object UserDetails { get; set; } // Will be Driver or Dispatcher object
    }
}
