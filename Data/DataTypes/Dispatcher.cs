namespace DispatchApp.Server.Data.DataTypes
{
    public class Dispatcher
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public DateTime DateJoined { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsAdmin { get; set; }
        public string Email { get; set; }
    }
}
