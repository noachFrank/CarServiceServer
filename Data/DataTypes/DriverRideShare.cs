namespace DispatchApp.Server.Data.DataTypes
{
    public class DriverRideShare
    {
        public Driver Driver { get; set; }
        public Car PrimaryCar { get; set; }
        public Ride CurrentRide { get; set; }
        public List<Ride> UpcomingRides { get; set; }
        public bool IsOnline { get; set; }
    }
}
