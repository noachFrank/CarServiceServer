public class LoginRequest
{
    public string UserType { get; set; }
    public string NameOrEmail{ get; set; }
    public string Password { get; set; }
}
public class LogoutRequest
{
    public string UserType { get; set; }
    public int UserId { get; set; }
}