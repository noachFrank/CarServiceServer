using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Car
    {
        public int CarId { get; set; }
        public int DriverId { get; set; }
        public CarType Type { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public int Seats { get; set; }
        public string Color { get; set; }
        public string LicensePlate { get; set; }
        public bool IsPrimary { get; set; }
        [JsonIgnore]
        public Driver? Driver { get; set; }
    }

    public class SetPrimaryCarRequest
    {
        public int CarId { get; set; }
    }
}
