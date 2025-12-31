using DispatchApp.Server.Data.DataRepositories;
using DispatchApp.Server.Data.DataTypes;
using Microsoft.Extensions.Configuration;

namespace DispatchApp.Server.Services
{
    /// <summary>
    /// Service to check if a driver is available to take a new call based on their current schedule.
    /// 
    /// HOW IT WORKS:
    /// 1. Each call has a "time window" = ScheduledFor → ScheduledFor + EstimatedDuration + GracePeriod
    /// 2. Grace period scales based on ride length (longer rides = more buffer time)
    /// 3. Two calls conflict if their time windows overlap
    /// 4. We also add travel time from one call's dropoff to the next call's pickup (using Google Maps)
    /// 5. Only checks rides that haven't been dropped off yet (if you just finished a ride, you're free!)
    /// </summary>
    public class DriverAvailabilityService
    {
        private readonly string _connectionString;
        private readonly GoogleMapsService _googleMapsService;

        // Configuration values (loaded from appsettings.json)
        private readonly int _defaultTravelTimeMinutes;
        private readonly int _baseGracePeriodMinutes;
        private readonly int _longCallThresholdMinutes;
        private readonly bool _gracePeriodScalingEnabled;

        /// <summary>
        /// Constructor that loads settings from configuration.
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="configuration">App configuration (for reading appsettings.json)</param>
        public DriverAvailabilityService(string connectionString, IConfiguration configuration)
        {
            _connectionString = connectionString;

            // Load Google Maps API key from config
            var googleMapsApiKey = configuration["GoogleMaps:ApiKey"] ?? "";
            _googleMapsService = new GoogleMapsService(googleMapsApiKey);

            // Load availability settings from config (with defaults if not specified)
            _defaultTravelTimeMinutes = configuration.GetValue<int>("DriverAvailability:DefaultTravelTimeMinutes", 20);
            _baseGracePeriodMinutes = configuration.GetValue<int>("DriverAvailability:BaseGracePeriodMinutes", 30);
            _longCallThresholdMinutes = configuration.GetValue<int>("DriverAvailability:LongCallThresholdMinutes", 45);
            _gracePeriodScalingEnabled = configuration.GetValue<bool>("DriverAvailability:GracePeriodScalingEnabled", true);

            Console.WriteLine($"DriverAvailability Settings: DefaultTravel={_defaultTravelTimeMinutes}min, BaseGrace={_baseGracePeriodMinutes}min, LongCallThreshold={_longCallThresholdMinutes}min, ScalingEnabled={_gracePeriodScalingEnabled}");
        }

        /// <summary>
        /// Constructor with explicit settings (for testing or when config not available).
        /// </summary>
        public DriverAvailabilityService(string connectionString, string googleMapsApiKey = "",
            int defaultTravelTimeMinutes = 20, int baseGracePeriodMinutes = 30,
            int longCallThresholdMinutes = 45, bool gracePeriodScalingEnabled = true)
        {
            _connectionString = connectionString;
            _googleMapsService = new GoogleMapsService(googleMapsApiKey);
            _defaultTravelTimeMinutes = defaultTravelTimeMinutes;
            _baseGracePeriodMinutes = baseGracePeriodMinutes;
            _longCallThresholdMinutes = longCallThresholdMinutes;
            _gracePeriodScalingEnabled = gracePeriodScalingEnabled;
        }

        /// <summary>
        /// Check if a driver can take a new call based on their current schedule.
        /// 
        /// Returns TRUE if the driver is available (no conflicts).
        /// Returns FALSE if any of their active calls would overlap with the new call.
        /// </summary>
        /// <param name="driverId">The driver to check</param>
        /// <param name="newCall">The new call we want to assign</param>
        /// <returns>True if driver is available, false if they have conflicts</returns>
        public bool IsDriverAvailableForCall(int driverId, Ride newCall)
        {
            var rideRepo = new RideRepo(_connectionString);

            // Step 1: Get all of this driver's active calls
            var activeRides = rideRepo.GetUpcomingRidesByDriver(driverId);
            activeRides = activeRides.Where(r => r.DropOffTime == null).ToList();

            // If driver has no active calls, they're definitely available
            if (activeRides == null || !activeRides.Any())
            {
                // Don't log for every single ride check - too verbose
                return true;
            }

            Console.WriteLine($"Driver {driverId}: Checking {activeRides.Count} active calls for conflicts with new call at {newCall.ScheduledFor:HH:mm}");

            // Step 2: Calculate the time window for the NEW call
            var newCallWindow = CalculateCallTimeWindow(newCall);
            Console.WriteLine($"  New call window: {newCallWindow.Start:HH:mm} - {newCallWindow.End:HH:mm} (grace: {newCallWindow.GraceMinutes}min)");

            // Step 3: Check each active call for overlap
            foreach (var activeRide in activeRides)
            {
                // Skip rides that have already been dropped off (double-check)
                if (activeRide.DropOffTime != null)
                {
                    Console.WriteLine($"  Skipping ride {activeRide.RideId}: Already dropped off at {activeRide.DropOffTime:HH:mm}");
                    continue;
                }

                // Skip rides that are more than 2 days apart - they can't possibly conflict
                var daysDifference = Math.Abs((activeRide.ScheduledFor.Date - newCall.ScheduledFor.Date).TotalDays);
                if (daysDifference > 2)
                {
                    Console.WriteLine($"  Skipping ride {activeRide.RideId}: {daysDifference} days apart (no conflict possible)");
                    continue;
                }

                // Calculate the time window for this active call
                var activeCallWindow = CalculateCallTimeWindow(activeRide);

                // Get travel time from call's dropoff to the other call's pickup
                // Uses Google Maps API for accurate travel time, falls back to default if API fails
                int travelTimeToNew = GetTravelTimeBetweenLocations(
                    activeRide.Route?.DropOff,
                    newCall.Route?.Pickup
                );
                int travelTimeToActive = GetTravelTimeBetweenLocations(
                  newCall.Route?.DropOff,
                  activeRide.Route?.Pickup
              );

                // Extend each call's end time by travel time and grace period
                var effectiveActiveEndTime = activeCallWindow.End.AddMinutes(travelTimeToNew).AddMinutes(activeCallWindow.GraceMinutes);
                var effectiveNewEndTime = newCallWindow.End.AddMinutes(travelTimeToActive).AddMinutes(newCallWindow.GraceMinutes);

                //Console.WriteLine($"  Active call {activeRide.RideId}: {activeCallWindow.Start:HH:mm} - {activeCallWindow.End:HH:mm} (grace: {activeCallWindow.GraceMinutes}min) + {travelTimeMinutes}min travel = effective end {effectiveEndTime:HH:mm}");

                // Check if windows overlap
                // Two windows overlap if: StartA < EndB AND StartB < EndA
                bool hasConflict = newCallWindow.Start < effectiveActiveEndTime && activeCallWindow.Start < effectiveNewEndTime;

                if (hasConflict)
                {
                    Console.WriteLine($"  ❌ CONFLICT detected with call {activeRide.RideId}");
                    return false; // Driver is NOT available
                }
            }

            // No conflicts found
            Console.WriteLine($"  ✅ No conflicts found, driver is available");
            return true;
        }

        /// <summary>
        /// Calculate the time window a call occupies, including a grace period.
        /// 
        /// Time Window = [ScheduledFor] to [ScheduledFor + EstimatedDuration + GracePeriod]
        /// 
        /// GRACE PERIOD SCALING (when enabled):
        /// - Rides under 45 min: No grace period
        /// - Rides 45-60 min: 30 min grace period (base)
        /// - Rides 60-90 min: 35 min grace period
        /// - Rides 90-120 min: 40 min grace period
        /// - Rides over 120 min: 45 min grace period
        /// 
        /// Why? Longer trips are less predictable - traffic, delays, etc.
        /// </summary>
        private (DateTime Start, DateTime End, int GraceMinutes) CalculateCallTimeWindow(Ride ride)
        {
            DateTime startTime = ride.ScheduledFor;

            // Convert EstimatedDuration (TimeOnly) to total minutes
            // EstimatedDuration is stored as a TimeOnly, like "01:30" meaning 1 hour 30 minutes
            int durationMinutes = ride.Route.EstimatedDuration.Hour * 60 + ride.Route.EstimatedDuration.Minute;

            // Calculate grace period
            int gracePeriodMinutes = CalculateGracePeriod(durationMinutes);

            // Calculate end time
            DateTime endTime = startTime.AddMinutes(durationMinutes + gracePeriodMinutes);

            return (startTime, endTime, gracePeriodMinutes);
        }

        /// <summary>
        /// Calculate the grace period based on ride duration.
        /// 
        /// The logic:
        /// - Short rides (under threshold): No grace needed, they're predictable
        /// - Long rides: Base grace period + extra time based on how long the ride is
        /// 
        /// Formula for long rides:
        /// grace = baseGrace + (durationMinutes - threshold) / 30 * 5
        /// 
        /// Examples (with default settings: base=30, threshold=45):
        /// - 30 min ride → 0 min grace (under threshold)
        /// - 45 min ride → 30 min grace (at threshold)
        /// - 60 min ride → 32 min grace (30 + 5*(60-45)/30 = 30 + 2.5 ≈ 32)
        /// - 90 min ride → 37 min grace (30 + 5*(90-45)/30 = 30 + 7.5 ≈ 37)
        /// - 120 min ride → 42 min grace (30 + 5*(120-45)/30 = 30 + 12.5 ≈ 42)
        /// </summary>
        private int CalculateGracePeriod(int durationMinutes)
        {
            // If ride is under the threshold, no grace period needed
            if (durationMinutes < _longCallThresholdMinutes)
            {
                return 0;
            }

            // If scaling is disabled, just return the base grace period
            if (!_gracePeriodScalingEnabled)
            {
                return _baseGracePeriodMinutes;
            }

            // Scale the grace period based on ride length
            // For every 30 minutes over the threshold, add 5 more minutes of grace
            int extraMinutesOverThreshold = durationMinutes - _longCallThresholdMinutes;
            int additionalGrace = (int)Math.Ceiling(extraMinutesOverThreshold / 30.0 * 5);

            // Cap the grace period at 60 minutes (don't want it to be too long)
            int totalGrace = Math.Min(_baseGracePeriodMinutes + additionalGrace, 60);

            return totalGrace;
        }

        /// <summary>
        /// Get travel time between two locations.
        /// 
        /// First tries Google Maps API for accurate travel time.
        /// Falls back to default if API fails or addresses are missing.
        /// </summary>
        /// <param name="fromAddress">The starting address (dropoff of previous call)</param>
        /// <param name="toAddress">The destination address (pickup of next call)</param>
        /// <returns>Travel time in minutes</returns>
        private int GetTravelTimeBetweenLocations(string? fromAddress, string? toAddress)
        {
            // If either address is missing, use default travel time
            if (string.IsNullOrEmpty(fromAddress) || string.IsNullOrEmpty(toAddress))
            {
                Console.WriteLine($"  Missing address, using default travel time: {_defaultTravelTimeMinutes}min");
                return _defaultTravelTimeMinutes;
            }

            // Try to get actual travel time from Google Maps
            try
            {
                int travelTime = _googleMapsService.GetTravelTimeMinutes(fromAddress, toAddress);

                if (travelTime > 0)
                {
                    return travelTime;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Google Maps error: {ex.Message}");
            }

            // Fall back to default
            Console.WriteLine($"  Using default travel time: {_defaultTravelTimeMinutes}min");
            return _defaultTravelTimeMinutes;
        }

        /// <summary>
        /// Filter a list of drivers to only those who are available for a specific call.
        /// This is the main method to call from the SignalR hub.
        /// </summary>
        /// <param name="drivers">List of potential drivers</param>
        /// <param name="newCall">The call we want to assign</param>
        /// <returns>List of drivers who are available (no schedule conflicts)</returns>
        public List<Driver> FilterAvailableDrivers(List<Driver> drivers, Ride newCall)
        {
            var availableDrivers = new List<Driver>();

            foreach (var driver in drivers)
            {
                if (IsDriverAvailableForCall(driver.Id, newCall))
                {
                    availableDrivers.Add(driver);
                }
            }

            Console.WriteLine($"Filtered {drivers.Count} drivers down to {availableDrivers.Count} available drivers for call at {newCall.ScheduledFor:HH:mm}");

            return availableDrivers;
        }
    }
}
