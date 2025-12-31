using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Recurring
    {
        public int Id { get; set; }
        public int DayOfWeek { get; set; }
        public TimeOnly Time { get; set; }
        public DateTime EndDate { get; set; }
        [JsonIgnore]
        public ICollection<Ride>? Rides { get; set; }

    }
}
