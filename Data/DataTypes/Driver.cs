using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Driver
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string License { get; set; }
        public DateTime? JoinedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool Active { get; set; }
        public bool OnJob { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string? ExpoPushToken { get; set; }

        public ICollection<Car>? Cars { get; set; }

        [JsonIgnore]
        public ICollection<Ride>? AssignedRides { get; set; }

        [JsonIgnore]
        public ICollection<Ride>? ReassignedRides { get; set; }

        [JsonIgnore]
        public ICollection<Communication>? Communications { get; set; }

    }
}
