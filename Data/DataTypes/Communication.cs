using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Communication
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public int DriverId { get; set; }
        public string From { get; set; }
        public DateTime Date { get; set; }
        public bool Read { get; set; }

        [JsonIgnore]
        public Driver? Driver { get; set; } // Navigation property

    }
}
