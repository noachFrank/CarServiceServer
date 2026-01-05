using System.Collections.Generic;

namespace DispatchApp.Server.Data.DataTypes
{
    public class DashboardMetrics
    {
        public List<Ride> AssignedRides { get; set; }
        public List<Ride> OpenRides { get; set; }
        public List<Ride> RidesInProgress { get; set; }
        public List<Ride> RecurringRidesThisWeek { get; set; }
        public List<Ride> TodaysRides { get; set; }
        public List<Driver> ActiveDrivers { get; set; }
        public List<Driver> DriversOnJob { get; set; }
        public List<Driver> UnsettledDrivers { get; set; }
    }
}
