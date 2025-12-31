using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Routes
    {
        public int RouteId { get; set; }
        public string Pickup { get; set; }
        public string DropOff { get; set; }
        public string? Stop1 { get; set; }
        public string? Stop2 { get; set; }
        public string? Stop3 { get; set; }
        public string? Stop4 { get; set; }
        public string? Stop5 { get; set; }
        public string? Stop6 { get; set; }
        public string? Stop7 { get; set; }
        public string? Stop8 { get; set; }
        public string? Stop9 { get; set; }
        public string? Stop10 { get; set; }

        public bool RoundTrip { get; set; }
        public TimeOnly EstimatedDuration { get; set; }

        [JsonIgnore]
        public List<Ride> Rides { get; set; }
    }
}
