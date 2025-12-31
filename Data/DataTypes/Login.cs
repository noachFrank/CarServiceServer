namespace DispatchApp.Server.Data.DataTypes
{
    public class Login
    {
        public int Id { get; set; }
        public string WorkerType { get; set; }
        public int WorkerId { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
        public int CallsTaken { get; set; } 
    }
}
