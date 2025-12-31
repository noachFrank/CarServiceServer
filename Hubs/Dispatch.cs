using DispatchApp.Server.Data.DataRepositories;
using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;

namespace DispatchApp.Server.Hubs
{
    [Authorize] // Require authentication for SignalR connections
    public class Dispatch : Hub
    {
        // Store active connections - ALL connected drivers (for messaging)
        private static readonly ConcurrentDictionary<string, string> _driverConnections = new();
        private static readonly ConcurrentDictionary<string, string> _dispatcherConnections = new();
        private static readonly ConcurrentDictionary<string, DriverStatus> _driverStatuses = new();

        // Store real-time driver locations for map tracking
        private static readonly ConcurrentDictionary<int, DriverLocation> _driverLocations = new();

        // Heartbeat tracking - driver must send heartbeat to stay "active" for receiving calls
        // Drivers can still receive messages even without heartbeat
        private static readonly ConcurrentDictionary<string, DateTime> _driverHeartbeats = new();
        private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(15); // Mark inactive after 15 minutes without heartbeat
        private static Timer? _heartbeatChecker;
        private static readonly object _heartbeatLock = new();

        // Push notification service for sending notifications to drivers who aren't connected via SignalR
        // This ensures drivers receive notifications even when the app is in the background or closed
        private readonly PushNotificationService _pushNotificationService;

        private string _connectionString;
        private IConfiguration _configuration;

        public Dispatch(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("ConStr");
            _configuration = config;
            _pushNotificationService = new PushNotificationService(_connectionString);

            // Start heartbeat checker if not already running
            lock (_heartbeatLock)
            {
                if (_heartbeatChecker == null)
                {
                    _heartbeatChecker = new Timer(CheckHeartbeats, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                }
            }
        }

        /// <summary>
        /// Check for drivers who have missed heartbeats and mark them inactive in database
        /// Note: This only affects "active for calls" status, not SignalR connection (they can still receive messages)
        /// </summary>
        private void CheckHeartbeats(object? state)
        {
            var now = DateTime.Now;
            var expiredDrivers = _driverHeartbeats
                .Where(kvp => now - kvp.Value > HeartbeatTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var driverId in expiredDrivers)
            {
                Console.WriteLine($"Driver {driverId} heartbeat expired - marking inactive in database");
                _driverHeartbeats.TryRemove(driverId, out _);
                _driverStatuses.TryRemove(driverId, out _);
                // Note: Do NOT remove from _driverConnections - they can still receive messages

                // Update database to mark driver as inactive
                try
                {
                    if (int.TryParse(driverId, out int driverIdInt))
                    {
                        var userRepo = new UserRepo(_connectionString);
                        userRepo.SetDriverActiveStatus(driverIdInt, false);
                        Console.WriteLine($"Driver {driverId} marked inactive in database");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating driver {driverId} status in database: {ex.Message}");
                }
            }

            if (expiredDrivers.Any())
            {
                Console.WriteLine($"Marked {expiredDrivers.Count} drivers as inactive");
            }
        }

        #region Connection Management

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var transport = httpContext?.Features.Get<Microsoft.AspNetCore.Http.Connections.Features.IHttpTransportFeature>()?.TransportType;
            var remoteIp = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "unknown";

            Console.WriteLine($"🔌 Client connected:");
            Console.WriteLine($"   ConnectionId: {Context.ConnectionId}");
            Console.WriteLine($"   Transport: {transport}");
            Console.WriteLine($"   Remote IP: {remoteIp}");
            Console.WriteLine($"   User-Agent: {userAgent}");

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Driver sends heartbeat to indicate they're still active
        /// Should be called every 30 seconds from the app
        /// </summary>
        public async Task DriverHeartbeat(string driverId)
        {
            // Check if this is a new driver becoming active (wasn't in heartbeats before)
            bool wasInactive = !_driverHeartbeats.ContainsKey(driverId);

            _driverHeartbeats.AddOrUpdate(driverId, DateTime.Now, (key, oldValue) => DateTime.Now);

            // Also update the connection ID in case it changed
            _driverConnections.AddOrUpdate(driverId, Context.ConnectionId, (key, oldValue) => Context.ConnectionId);

            // If driver just became active, update the database
            if (wasInactive)
            {
                Console.WriteLine($"Driver {driverId} became active via heartbeat");
                try
                {
                    if (int.TryParse(driverId, out int driverIdInt))
                    {
                        var userRepo = new UserRepo(_connectionString);
                        userRepo.SetDriverActiveStatus(driverIdInt, true);
                        Console.WriteLine($"Driver {driverId} marked active in database");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating driver {driverId} status in database: {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Driver sends their current GPS location for map tracking.
        /// This is called frequently (every few seconds) when driver has an active ride.
        /// The location is stored in memory and broadcasted to all connected dispatchers.
        /// </summary>
        /// <param name="driverId">The driver's ID</param>
        /// <param name="latitude">GPS latitude</param>
        /// <param name="longitude">GPS longitude</param>
        /// <param name="rideId">Optional current ride ID</param>
        public async Task UpdateDriverLocation(int driverId, double latitude, double longitude, int? rideId = null)
        {
            try
            {
                // Get driver info and current ride info from database
                var userRepo = new UserRepo(_connectionString);
                var driver = userRepo.GetDriverById(driverId);

                string? pickup = null;
                string? dropoff = null;
                string? customerName = null;

                if (rideId.HasValue)
                {
                    var rideRepo = new RideRepo(_connectionString);
                    var ride = rideRepo.GetById(rideId.Value);
                    if (ride != null)
                    {
                        pickup = ride.Route?.Pickup;
                        dropoff = ride.Route?.DropOff;
                        customerName = ride.CustomerName;
                    }
                }

                var location = new DriverLocation
                {
                    DriverId = driverId,
                    DriverName = driver?.Name ?? "Unknown",
                    Latitude = latitude,
                    Longitude = longitude,
                    LastUpdate = DateTime.Now,
                    CurrentRideId = rideId,
                    CurrentPickup = pickup,
                    CurrentDropoff = dropoff,
                    CustomerName = customerName
                };

                // Store/update location in memory
                _driverLocations.AddOrUpdate(driverId, location, (key, oldValue) => location);

                // Broadcast to all connected dispatchers
                await Clients.Group("dispatchers").SendAsync("DriverLocationUpdated", location);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating driver location: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove driver location when they complete a ride or go offline
        /// </summary>
        public async Task RemoveDriverLocation(int driverId)
        {
            _driverLocations.TryRemove(driverId, out _);
            await Clients.Group("dispatchers").SendAsync("DriverLocationRemoved", driverId);
        }

        /// <summary>
        /// Get all currently tracked driver locations (called when dispatcher first loads the map)
        /// </summary>
        public async Task<List<DriverLocation>> GetAllDriverLocations()
        {
            return _driverLocations.Values.ToList();
        }

        /// <summary>
        /// Get count of currently active drivers (those with recent heartbeats)
        /// </summary>
        public async Task<int> GetActiveDriverCount()
        {
            var now = DateTime.Now;
            var activeCount = _driverHeartbeats.Count(kvp => now - kvp.Value <= HeartbeatTimeout);
            return activeCount;
        }

        /// <summary>
        /// Get list of active driver IDs
        /// </summary>
        public async Task<List<string>> GetActiveDriverIds()
        {
            var now = DateTime.Now;
            return _driverHeartbeats
                .Where(kvp => now - kvp.Value <= HeartbeatTimeout)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Static method to get online driver IDs - can be called from controllers
        /// </summary>
        public static List<int> GetOnlineDriverIds()
        {
            var now = DateTime.Now;
            return _driverHeartbeats
                .Where(kvp => now - kvp.Value <= HeartbeatTimeout)
                .Select(kvp => int.TryParse(kvp.Key, out int id) ? id : -1)
                .Where(id => id > 0)
                .ToList();
        }

        /// <summary>
        /// Static method to check if a specific driver is online
        /// </summary>
        public static bool IsDriverOnline(int driverId)
        {
            var now = DateTime.Now;
            if (_driverHeartbeats.TryGetValue(driverId.ToString(), out DateTime lastHeartbeat))
            {
                return now - lastHeartbeat <= HeartbeatTimeout;
            }
            return false;
        }

        public async Task RegisterDriver(string driverId)
        {
            _driverConnections.AddOrUpdate(driverId, Context.ConnectionId, (key, oldValue) => Context.ConnectionId);
            _driverStatuses.AddOrUpdate(driverId, new DriverStatus
            {
                DriverId = driverId,
                Status = "available",
                LastUpdate = DateTime.Now
            }, (key, oldValue) => new DriverStatus
            {
                DriverId = driverId,
                Status = "available",
                LastUpdate = DateTime.Now
            });

            // Start heartbeat tracking
            bool wasInactive = !_driverHeartbeats.ContainsKey(driverId);
            _driverHeartbeats.AddOrUpdate(driverId, DateTime.Now, (key, oldValue) => DateTime.Now);

            // If driver just became active, update the database
            if (wasInactive)
            {
                Console.WriteLine($"Driver {driverId} became active via registration");
                try
                {
                    if (int.TryParse(driverId, out int driverIdInt))
                    {
                        var userRepo = new UserRepo(_connectionString);
                        userRepo.SetDriverActiveStatus(driverIdInt, true);
                        Console.WriteLine($"Driver {driverId} marked active in database");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating driver {driverId} status in database: {ex.Message}");
                }
            }

            await Clients.Caller.SendAsync("Connected", new { message = $"Driver {driverId} registered successfully" });
            Console.WriteLine($"Driver {driverId} registered with connection {Context.ConnectionId}");
        }

        public async Task UnregisterDriver(string driverId)
        {
            _driverConnections.TryRemove(driverId, out _);
            _driverStatuses.TryRemove(driverId, out _);
            _driverHeartbeats.TryRemove(driverId, out _);
            Console.WriteLine($"Driver {driverId} unregistered");
            if (int.TryParse(driverId, out int driverIdInt))
            {
                var userRepo = new UserRepo(_connectionString);
                userRepo.SetDriverActiveStatus(driverIdInt, false);
                Console.WriteLine($"Driver {driverId} set to inactive in database upon unregistering");
            }
            await Task.CompletedTask;
        }

        public async Task RegisterDispatcher(string dispatcherId)
        {
            Console.WriteLine($"🔵 RegisterDispatcher called: dispatcherId={dispatcherId}, connectionId={Context.ConnectionId}");
            _dispatcherConnections.AddOrUpdate(dispatcherId, Context.ConnectionId, (key, oldValue) => Context.ConnectionId);
            Console.WriteLine($"📊 Total dispatchers connected: {_dispatcherConnections.Count}");
            Console.WriteLine($"📋 Dispatcher connections: {string.Join(", ", _dispatcherConnections.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            // Add dispatcher to "dispatchers" group for receiving driver location updates
            await Groups.AddToGroupAsync(Context.ConnectionId, "dispatchers");

            await Clients.Caller.SendAsync("Connected", new { message = $"Dispatcher {dispatcherId} registered successfully" });
            Console.WriteLine($"✅ Dispatcher {dispatcherId} registered with connection {Context.ConnectionId}");
        }

        public async Task UpdateDriverStatus(DriverStatusUpdate update)
        {
            if (_driverStatuses.TryGetValue(update.DriverId, out var status))
            {
                status.Status = update.Status;
                status.LastUpdate = DateTime.Parse(update.Timestamp);
                Console.WriteLine($"Driver {update.DriverId} status updated to {update.Status}");
            }
            await Task.CompletedTask;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // For drivers: Don't immediately remove - let heartbeat system handle it
            // This allows drivers to briefly lose connection (e.g., switching apps) without being marked inactive
            var driver = _driverConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(driver.Key))
            {
                // Only remove the connection mapping, NOT the heartbeat
                // Driver will be marked inactive only if heartbeat expires
                _driverConnections.TryRemove(driver.Key, out _);
                Console.WriteLine($"Driver {driver.Key} connection dropped (heartbeat still active for {HeartbeatTimeout.TotalSeconds}s)");
            }

            // Remove from dispatchers immediately (they use the web app, different behavior)
            var dispatcher = _dispatcherConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(dispatcher.Key))
            {
                _dispatcherConnections.TryRemove(dispatcher.Key, out _);
                Console.WriteLine($"Dispatcher {dispatcher.Key} disconnected");
            }

            await base.OnDisconnectedAsync(exception);
        }

        #endregion

        #region Call Management Sockets

        /// <summary>
        /// Determines if a driver's car can handle a specific ride based on car type and seat count.
        /// Logic mirrors GetOpenRidesNotAssigned(int userId) in RideRepo.cs
        /// 
        /// Car Type Hierarchy (based on CarType enum order):
        /// - Car (0): Only handles Car requests
        /// - SUV (1): Handles Car, SUV requests  
        /// - MiniVan (2): Handles Car, SUV, MiniVan requests
        /// - TwelvePass (3): Handles Car, SUV, MiniVan, TwelvePass requests
        /// - FifteenPass (4): Handles Car, SUV, MiniVan, TwelvePass, FifteenPass requests
        /// - LuxurySUV (5): Handles Car, SUV, LuxurySUV requests (special case - NOT MiniVan/TwelvePass/FifteenPass)
        /// 
        /// Special LuxurySUV rules:
        /// - LuxurySUV calls can ONLY go to LuxurySUV drivers
        /// - LuxurySUV drivers can see Car, SUV, and LuxurySUV calls (but not MiniVan, TwelvePass, FifteenPass)
        /// </summary>
        private bool CanDriverHandleRide(Car driverCar, Ride ride)
        {
            if (driverCar == null)
                return true; // If no primary car, allow all rides (fallback behavior)

            // Check seat capacity
            if (ride.Passengers > driverCar.Seats)
                return false;

            // Special LuxurySUV logic:
            // - LuxurySUV calls can ONLY go to LuxurySUV drivers
            // - LuxurySUV drivers can handle Car, SUV, and LuxurySUV (but NOT MiniVan, TwelvePass, FifteenPass)
            if (ride.CarType == CarType.LuxurySUV)
            {
                return driverCar.Type == CarType.LuxurySUV;
            }

            // For non-LuxurySUV rides: driver's car type must be >= requested car type
            // LuxurySUV drivers can handle Car and SUV but not MiniVan/TwelvePass/FifteenPass
            if (driverCar.Type == CarType.LuxurySUV)
            {
                // LuxurySUV drivers can only handle Car and SUV (not MiniVan or larger)
                return ride.CarType <= CarType.SUV;
            }

            // Standard car type comparison: higher enum value = can handle lower types
            return ride.CarType <= driverCar.Type;
        }

        // Socket for when a new call comes in
        // If pre-assigned, only notify the specific driver
        // Otherwise, broadcast to all available drivers whose car can handle the ride
        public async Task NewCallCreated(Ride newRide, Recurring recurring = null)
        {
            try
            {
                // Update database first
                var rideRepo = new RideRepo(_connectionString);
                var recText = "";
                if (recurring != null)
                {
                    //reccurring.RideId = newRide.RideId;
                    rideRepo.AddRecurring(recurring);
                    newRide.IsRecurring = true;
                    newRide.RecurringId = recurring.Id;
                    recText = "Reccurring ";
                }
                rideRepo.AddRide(newRide);

                var userRepo = new UserRepo(_connectionString);

                // Format the scheduled time for the notification
                var scheduledTime = newRide.ScheduledFor.ToString("h:mm tt");
                var scheduledDate = newRide.ScheduledFor.Date == DateTime.Today
                    ? "Today"
                    : newRide.ScheduledFor.Date == DateTime.Today.AddDays(1)
                        ? "Tomorrow"
                        : newRide.ScheduledFor.ToString("MMM d");

                // Check if this call is pre-assigned to a specific driver
                if (newRide.AssignedToId != null && newRide.AssignedToId > 0)
                {
                    // Pre-assigned call - only notify the specific driver
                    var assignedDriverId = newRide.AssignedToId.Value.ToString();

                    if (_driverConnections.TryGetValue(assignedDriverId, out var connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("NewCallAvailable", newRide);
                        Console.WriteLine($"Pre-assigned call {newRide.RideId} sent to driver {assignedDriverId}");
                    }
                    else
                    {
                        Console.WriteLine($"Pre-assigned driver {assignedDriverId} not connected - call saved to database");
                    }

                    // Also send push notification for pre-assigned call
                    var pushToken = userRepo.GetDriverPushToken(newRide.AssignedToId.Value);
                    if (!string.IsNullOrEmpty(pushToken))
                    {
                        await _pushNotificationService.SendPushNotificationAsync(
                            pushToken,
                           $"📍 New {recText}Call Assigned to You",
                            $"{newRide.Route?.Pickup ?? "Pickup"} → {newRide.Route?.DropOff ?? "Dropoff"}\n{scheduledDate} at {scheduledTime}",
                            new
                            {
                                type = "NEW_CALL",
                                rideId = newRide.RideId,
                                screen = "activeCalls"
                            },
                            newRide.AssignedToId.Value,
                            "NEW_CALL"
                        );
                    }

                    return;
                }

                // Not pre-assigned - broadcast to all available drivers whose car can handle the ride
                var availableDrivers = userRepo.GetAllDrivers();

                if (!availableDrivers.Any())
                {
                    Console.WriteLine("No available drivers to broadcast new call to");
                    return;
                }

                // Create availability service to check driver schedules
                var availabilityService = new DriverAvailabilityService(_connectionString, _configuration);

                // Filter drivers based on:
                // 1. Their primary car's compatibility with the ride
                // 2. Their schedule (no overlapping calls)
                var connectionIds = new List<string>();
                var eligibleDriverIds = new List<int>();

                foreach (var driver in availableDrivers)
                {
                    // Get driver's primary car
                    var primaryCar = userRepo.GetPrimaryCar(driver.Id);

                    // Check if driver's car can handle this ride
                    if (!CanDriverHandleRide(primaryCar, newRide))
                    {
                        Console.WriteLine($"Driver {driver.Id}: Car type {primaryCar?.Type} cannot handle ride requiring {newRide.CarType}");
                        continue;
                    }

                    // Check if driver is available (no schedule conflicts)
                    if (!availabilityService.IsDriverAvailableForCall(driver.Id, newRide))
                    {
                        Console.WriteLine($"Driver {driver.Id}: Has schedule conflict, skipping");
                        continue;
                    }

                    // Driver passes both checks - they're eligible
                    eligibleDriverIds.Add(driver.Id);

                    if (_driverConnections.TryGetValue(driver.Id.ToString(), out var connectionId))
                    {
                        connectionIds.Add(connectionId);
                    }
                }

                // Send SignalR notifications to connected drivers
                if (connectionIds.Any())
                {
                    await Clients.Clients(connectionIds).SendAsync("NewCallAvailable", newRide);
                    Console.WriteLine($"New call {newRide.RideId} (CarType: {newRide.CarType}, Passengers: {newRide.Passengers}) broadcasted to {connectionIds.Count} compatible drivers");
                }
                else
                {
                    Console.WriteLine($"No compatible connected drivers for call {newRide.RideId} (CarType: {newRide.CarType}, Passengers: {newRide.Passengers})");
                }

                // Send push notifications to ALL eligible drivers (even if connected via SignalR)
                // This ensures they get notified even if app is in background
                if (eligibleDriverIds.Any())
                {
                    var pushTokens = userRepo.GetDriverPushTokens(eligibleDriverIds);
                    if (pushTokens.Any())
                    {
                        var pushRequests = pushTokens.Select(kvp => new PushNotificationRequest
                        {
                            ExpoPushToken = kvp.Value,
                            Title = $"🚗 New {recText}Call Available",
                            Body = $"{newRide.Route?.Pickup ?? "Pickup"} → {newRide.Route?.DropOff ?? "Dropoff"}\n{scheduledDate} at {scheduledTime}",
                            Data = new
                            {
                                type = "NEW_CALL",
                                rideId = newRide.RideId,
                                screen = "openCalls"
                            }
                        }).ToList();

                        await _pushNotificationService.SendBatchPushNotificationsAsync(pushRequests);
                        Console.WriteLine($"Push notifications sent to {pushRequests.Count} drivers for new call {newRide.RideId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new call: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a recurring call - saves the ride and the recurring metadata
        /// The ride's scheduledFor is set to the first upcoming occurrence
        /// </summary>
        //public async Task CreateRecurringCall(Ride ride, Reoccurring reoccurring)
        //{
        //    try
        //    {
        //        // Add the ride first
        //        var rideRepo = new RideRepo(_connectionString);
        //        rideRepo.AddRide(ride);

        //        // Now add the recurring entry with the ride ID
        //        reoccurring.RideId = ride.RideId;
        //        var reoccurringRepo = new ReoccurringRepo(_connectionString);
        //        reoccurringRepo.AddReoccurring(reoccurring);

        //        Console.WriteLine($"✅ Recurring call created: RideId {ride.RideId}, Day {reoccurring.DayOfWeek}, Time {reoccurring.Time}, EndDate {reoccurring.EndDate}");

        //        // Broadcast the ride to drivers just like a regular call
        //        // This calls the same logic as NewCallCreated
        //        await NewCallCreated(ride);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error creating recurring call: {ex.Message}");
        //        throw;
        //    }
        //}


        // Socket for when a call is assigned - removes call from everyone else's screen

        public async Task CallAssigned(Assignment assignment)
        {
            try
            {
                // Update database first
                var rideRepo = new RideRepo(_connectionString);
                var ride = rideRepo.GetById(assignment.RideId);

                // Check if ride exists
                if (ride == null)
                {
                    Console.WriteLine($"Ride {assignment.RideId} not found");
                    await Clients.Caller.SendAsync("CallAlreadyAssigned", new
                    {
                        rideId = assignment.RideId,
                        message = "This ride no longer exists",
                        timestamp = DateTime.Now
                    });
                    return;
                }

                // Check if already assigned to someone else
                bool isAlreadyAssigned = (ride.AssignedToId != null && !ride.Reassigned) ||
                         (ride.Reassigned && ride.ReassignedToId != null);

                if (isAlreadyAssigned)
                {
                    Console.WriteLine($"Ride {assignment.RideId} already assigned to driver {ride.AssignedToId.Value}");
                    await Clients.Caller.SendAsync("CallAlreadyAssigned", new
                    {
                        rideId = assignment.RideId,
                        message = "This call has already been taken by another driver",
                        assignedToDriverId = ride.AssignedToId.Value,
                        timestamp = DateTime.Now
                    });
                    return;
                }

                // Proceed with assignment
                if (ride.Reassigned)
                    rideRepo.ReassignRide(ride.RideId, assignment.AssignToId);
                else
                    rideRepo.AssignRide(assignment.RideId, assignment.AssignToId);

                // Update driver status to on-call
                if (_driverStatuses.TryGetValue(assignment.AssignToId.ToString(), out var status))
                {
                    status.Status = "on-call";
                    status.LastUpdate = DateTime.Now;
                }

                // Notify the requesting driver that assignment was successful
                await Clients.Caller.SendAsync("CallAssignmentSuccess", new
                {
                    rideId = assignment.RideId,
                    message = "Call successfully assigned to you",
                    timestamp = DateTime.Now
                });

                // Notify all drivers that this call is no longer available
                var allDriverConnections = _driverConnections.Values.ToList();
                if (allDriverConnections.Any())
                {
                    await Clients.Clients(allDriverConnections).SendAsync("CallAssigned", new
                    {
                        rideId = assignment.RideId,
                        assignedToDriverId = assignment.AssignToId,
                        timestamp = DateTime.Now
                    });
                }

                // Notify all dispatchers about the assignment
                var dispatcherConnections = _dispatcherConnections.Values.ToList();
                if (dispatcherConnections.Any())
                {
                    await Clients.Clients(dispatcherConnections).SendAsync("CallAssigned", new
                    {
                        rideId = assignment.RideId,
                        assignedToDriverId = assignment.AssignToId,
                        timestamp = DateTime.Now
                    });
                }

                Console.WriteLine($"Call {assignment.RideId} assigned to driver {assignment.AssignToId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error assigning call: {ex.Message}");
                throw;
            }
        }

        public async Task CallDriverCancelled(int rideId)
        {
            try
            {
                // Get the ride details BEFORE updating to know which driver was assigned
                var rideRepo = new RideRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);
                var rideDataBefore = rideRepo.GetById(rideId);
                var previousDriverId = rideDataBefore?.Reassigned == true
                    ? rideDataBefore?.ReassignedToId
                    : rideDataBefore?.AssignedToId;

                Console.WriteLine($"CallDriverCancelled: rideId={rideId}, Reassigned={rideDataBefore?.Reassigned}, AssignedToId={rideDataBefore?.AssignedToId}, ReassignedToId={rideDataBefore?.ReassignedToId}, previousDriverId={previousDriverId}");

                // Update database - marks as reassigned
                rideRepo.CancelDriver(rideId);

                // Get the ride details to broadcast
                var rideData = rideRepo.GetById(rideId);
                if (rideData != null && rideData.Route != null)
                {
                    // Format the scheduled time for the notification
                    var scheduledTime = rideData.ScheduledFor.ToString("h:mm tt");
                    var scheduledDate = rideData.ScheduledFor.Date == DateTime.Today
                        ? "Today"
                        : rideData.ScheduledFor.Date == DateTime.Today.AddDays(1)
                            ? "Tomorrow"
                            : rideData.ScheduledFor.ToString("MMM d");

                    // Notify the driver who was removed from this call
                    if (previousDriverId.HasValue)
                    {
                        var driverIdStr = previousDriverId.Value.ToString();
                        Console.WriteLine($"Looking for driver connection with ID: {driverIdStr}");
                        Console.WriteLine($"Available driver connections: {string.Join(", ", _driverConnections.Keys)}");

                        if (_driverConnections.TryGetValue(driverIdStr, out var removedDriverConnectionId))
                        {
                            await Clients.Client(removedDriverConnectionId).SendAsync("CallUnassigned", new
                            {
                                rideId = rideId,
                                message = "You have been removed from this call by dispatch.",
                                timestamp = DateTime.Now
                            });
                            Console.WriteLine($"Notified driver {previousDriverId} that they were removed from call {rideId}");
                        }
                        else
                        {
                            Console.WriteLine($"Driver {previousDriverId} not found in connections dictionary");
                        }

                        // Send push notification to the removed driver
                        var removedDriverToken = userRepo.GetDriverPushToken(previousDriverId.Value);
                        if (!string.IsNullOrEmpty(removedDriverToken))
                        {
                            await _pushNotificationService.SendPushNotificationAsync(
                                removedDriverToken,
                                "⚠️ Call Reassigned",
                                $"You have been removed from the call:\n{rideData.Route.Pickup} → {rideData.Route.DropOff}",
                                new
                                {
                                    type = "CALL_UNASSIGNED",
                                    rideId = rideId,
                                    screen = "home"
                                },
                                previousDriverId.Value,
                                "CALL_UNASSIGNED"
                            );
                            Console.WriteLine($"Push notification sent to removed driver {previousDriverId}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No previous driver ID found for ride {rideId}");
                    }

                    // Get all ACTIVE drivers (not just available) whose car can handle this ride
                    // We use GetActiveDrivers instead of GetAvailableDrivers because a driver might
                    // already have a job but still want to see newly available calls
                    var availableDrivers = userRepo.GetAllDrivers();
                    Console.WriteLine($"Found {availableDrivers.Count} active drivers");

                    var connectionIds = new List<string>();
                    var eligibleDriverIds = new List<int>();

                    foreach (var driver in availableDrivers)
                    {
                        var primaryCar = userRepo.GetPrimaryCar(driver.Id);
                        if (CanDriverHandleRide(primaryCar, rideData))
                        {
                            Console.WriteLine($"Driver {driver.Id} ({driver.Name}) is eligible for reassigned call {rideId}");
                            eligibleDriverIds.Add(driver.Id);
                            if (_driverConnections.TryGetValue(driver.Id.ToString(), out var connectionId))
                            {
                                connectionIds.Add(connectionId);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Driver {driver.Id} ({driver.Name}) NOT eligible - CarType: {primaryCar?.Type}, Seats: {primaryCar?.Seats}, RideCarType: {rideData.CarType}, RidePassengers: {rideData.Passengers}");
                        }
                    }

                    Console.WriteLine($"Eligible drivers for reassigned call: {eligibleDriverIds.Count}, Connected: {connectionIds.Count}");

                    // Send SignalR notification
                    if (connectionIds.Any())
                    {
                        await Clients.Clients(connectionIds).SendAsync("CallAvailableAgain", rideData);
                        Console.WriteLine($"SignalR CallAvailableAgain sent to {connectionIds.Count} drivers");
                    }

                    // Send push notifications for call available again
                    if (eligibleDriverIds.Any())
                    {
                        var pushTokens = userRepo.GetDriverPushTokens(eligibleDriverIds);
                        Console.WriteLine($"Got push tokens for {pushTokens.Count} out of {eligibleDriverIds.Count} eligible drivers");

                        if (pushTokens.Any())
                        {
                            var pushRequests = pushTokens.Select(kvp => new PushNotificationRequest
                            {
                                ExpoPushToken = kvp.Value,
                                Title = "🔄 Call Available Again",
                                Body = $"{rideData.Route.Pickup} → {rideData.Route.DropOff}\n{scheduledDate} at {scheduledTime}",
                                Data = new
                                {
                                    type = "CALL_AVAILABLE_AGAIN",
                                    rideId = rideId,
                                    screen = "openCalls"
                                }
                            }).ToList();

                            await _pushNotificationService.SendBatchPushNotificationsAsync(pushRequests);
                            Console.WriteLine($"✅ Push notifications sent to {pushRequests.Count} drivers for reassigned call {rideId}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ No push tokens available for eligible drivers");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ No eligible drivers found for reassigned call {rideId}");
                    }

                    // Notify all dispatchers about the cancellation
                    var dispatcherConnections = _dispatcherConnections.Values.ToList();
                    if (dispatcherConnections.Any())
                    {
                        await Clients.Clients(dispatcherConnections).SendAsync("CallDriverCancelled", new
                        {
                            rideId = rideId,
                            timestamp = DateTime.Now
                        });
                    }
                }

                Console.WriteLine($"Driver cancelled from call {rideId}, call is now available again");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelling driver from call: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Called when a ride is completely canceled (not just driver removed).
        /// Notifies the assigned driver via SignalR and push notification.
        /// </summary>
        public async Task CallCanceled(int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);

                // Get ride details BEFORE canceling to know who was assigned
                var rideData = rideRepo.GetById(rideId);
                if (rideData == null)
                {
                    Console.WriteLine($"CallCanceled: Ride {rideId} not found");
                    return;
                }

                // Determine who was assigned to this call
                var assignedDriverId = rideData.Reassigned == true
                    ? rideData.ReassignedToId
                    : rideData.AssignedToId;

                // Cancel the ride in database
                rideRepo.CancelRide(rideId);

                // Notify the assigned driver if there was one
                if (assignedDriverId.HasValue)
                {
                    var driverIdStr = assignedDriverId.Value.ToString();

                    // Send SignalR notification if connected
                    if (_driverConnections.TryGetValue(driverIdStr, out var connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("CallCanceled", new
                        {
                            rideId = rideId,
                            message = "This call has been canceled.",
                            timestamp = DateTime.Now
                        });
                        Console.WriteLine($"CallCanceled SignalR notification sent to driver {assignedDriverId}");
                    }

                    // Send push notification
                    var pushToken = userRepo.GetDriverPushToken(assignedDriverId.Value);
                    if (!string.IsNullOrEmpty(pushToken))
                    {
                        await _pushNotificationService.SendPushNotificationAsync(
                            pushToken,
                            "❌ Call Canceled",
                            $"Your call has been canceled:\n{rideData.Route?.Pickup ?? "Pickup"} → {rideData.Route?.DropOff ?? "Dropoff"}",
                            new
                            {
                                type = "CALL_CANCELED",
                                rideId = rideId,
                                screen = "home"
                            },
                            assignedDriverId.Value,
                            "CALL_CANCELED"
                        );
                        Console.WriteLine($"Push notification sent to driver {assignedDriverId} for canceled call {rideId}");
                    }
                }

                // Notify all drivers to remove this call from their open calls list
                var allDriverConnections = _driverConnections.Values.ToList();
                if (allDriverConnections.Any())
                {
                    await Clients.Clients(allDriverConnections).SendAsync("CallCanceled", new
                    {
                        rideId = rideId,
                        message = "This call has been canceled.",
                        timestamp = DateTime.Now
                    });
                }

                // Notify all dispatchers
                var dispatcherConnections = _dispatcherConnections.Values.ToList();
                if (dispatcherConnections.Any())
                {
                    await Clients.Clients(dispatcherConnections).SendAsync("CallCanceled", new
                    {
                        rideId = rideId,
                        timestamp = DateTime.Now
                    });
                }

                Console.WriteLine($"Call {rideId} canceled and notifications sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error canceling call: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Called when a driver completes a ride (dropoff clicked)
        /// Broadcasts to all dispatchers so they can update their tracking maps
        /// </summary>
        public async Task RideCompleted(int rideId, int driverId)
        {
            try
            {
                Console.WriteLine($"Ride {rideId} completed by driver {driverId}");

                // Notify all dispatchers that a ride was completed
                await Clients.Group("dispatchers").SendAsync("RideCompleted", new
                {
                    rideId = rideId,
                    driverId = driverId,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting ride completion: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset pickup time for a ride and notify the assigned driver
        /// Called by dispatcher when they need to reset the pickup status
        /// </summary>
        public async Task ResetPickupTime(int rideId)
        {
            try
            {
                Console.WriteLine($"Resetting pickup time for ride {rideId}");

                // Get the ride details to find the assigned driver
                var rideRepo = new RideRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);
                var ride = rideRepo.GetById(rideId);

                if (ride == null)
                {
                    Console.WriteLine($"Ride {rideId} not found");
                    return;
                }

                // Determine which driver is assigned
                var assignedDriverId = ride.Reassigned == true ? ride.ReassignedToId : ride.AssignedToId;

                if (!assignedDriverId.HasValue)
                {
                    Console.WriteLine($"No driver assigned to ride {rideId}");
                    return;
                }

                // Reset the pickup time in database
                rideRepo.ResetPickupTime(rideId);
                Console.WriteLine($"✅ Pickup time reset for ride {rideId}");

                // Get updated ride data
                var updatedRide = rideRepo.GetById(rideId);

                // Notify the assigned driver
                var driverIdStr = assignedDriverId.Value.ToString();
                if (_driverConnections.TryGetValue(driverIdStr, out var driverConnectionId))
                {
                    await Clients.Client(driverConnectionId).SendAsync("PickupTimeReset", new
                    {
                        rideId = rideId,
                        message = "The pickup time for your ride has been reset by dispatch. Please pick up the customer again.",
                        timestamp = DateTime.Now
                    });
                    Console.WriteLine($"✅ Notified driver {assignedDriverId} about pickup time reset");
                }
                else
                {
                    Console.WriteLine($"⚠️ Driver {assignedDriverId} not connected via SignalR, sending push notification");

                    // Send push notification if driver is not connected
                    var pushToken = userRepo.GetDriverPushToken(assignedDriverId.Value);
                    if (!string.IsNullOrEmpty(pushToken))
                    {
                        await _pushNotificationService.SendPushNotificationAsync(
                            pushToken,
                            "Pickup Time Reset",
                            $"The pickup time for ride #{rideId} has been reset. Please pick up the customer again.",
                            new { type = "PICKUP_TIME_RESET", rideId = rideId, screen = "ActiveCalls" }
                        );
                        Console.WriteLine($"✅ Push notification sent to driver {assignedDriverId}");
                    }
                }

                // Notify all dispatchers about the update
                await Clients.Group("dispatchers").SendAsync("PickupTimeReset", new
                {
                    rideId = rideId,
                    driverId = assignedDriverId,
                    ride = updatedRide,
                    timestamp = DateTime.Now
                });
                Console.WriteLine($"✅ Notified all dispatchers about pickup time reset for ride {rideId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error resetting pickup time: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Messaging Sockets

        // Socket for when a dispatcher sends a message to a driver
        public async Task<Communication> DispatcherSendsMessage(DispatcherMessageData message)
        {
            try
            {
                // Save message to database first
                var communication = new Communication
                {
                    Message = message.Message,
                    DriverId = message.DriverId,
                    From = "Dispatcher",
                    Date = DateTime.UtcNow,
                    Read = false
                };

                var commRepo = new CommunicationRepo(_connectionString);
                commRepo.AddCom(communication);
                Console.WriteLine($"💾 Saved message to database with ID: {communication.Id}");

                // Send to specific driver if connected via SignalR
                if (_driverConnections.TryGetValue(message.DriverId.ToString(), out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", communication);
                    Console.WriteLine($"Message sent from dispatcher {message.DispatcherId} to driver {message.DriverId}: {message.Message}");
                }
                else
                {
                    Console.WriteLine($"Driver {message.DriverId} not connected - message saved to database only");
                }

                // Always send push notification for direct messages (not broadcasts)
                // This ensures driver gets notified even if app is in background
                var userRepo = new UserRepo(_connectionString);
                var pushToken = userRepo.GetDriverPushToken(message.DriverId);
                if (!string.IsNullOrEmpty(pushToken))
                {
                    // Truncate message for notification preview (max 100 chars)
                    var previewMessage = message.Message.Length > 100
                        ? message.Message.Substring(0, 97) + "..."
                        : message.Message;

                    await _pushNotificationService.SendPushNotificationAsync(
                        pushToken,
                        "💬 New Message from Dispatch",
                        previewMessage,
                        new
                        {
                            type = "NEW_MESSAGE",
                            messageId = communication.Id,
                            screen = "messages"
                        },
                        message.DriverId,
                        "NEW_MESSAGE"
                    );
                    Console.WriteLine($"Push notification sent for message to driver {message.DriverId}");
                }

                // Notify other dispatchers that message was sent
                var otherDispatchers = _dispatcherConnections.Values
                    .Where(conn => conn != Context.ConnectionId).ToList();

                if (otherDispatchers.Any())
                {
                    await Clients.Clients(otherDispatchers).SendAsync("MessageSent", new
                    {
                        messageId = communication.Id,
                        message = message.Message,
                        fromDispatcherId = message.DispatcherId,
                        toDriverId = message.DriverId,
                        rideId = message.RideId,
                        timestamp = DateTime.Now
                    });
                }

                // Return the communication object with its database ID
                return communication;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message from dispatcher to driver: {ex.Message}");
                throw;
            }
        }

        // Socket for when a driver sends a message to dispatchers
        public async Task<Communication> DriverSendsMessage(DriverMessageData message)
        {
            try
            {
                // Save message to database first
                var communication = new Communication
                {
                    Message = message.Message,
                    DriverId = message.DriverId,
                    From = "Driver",
                    Date = DateTime.UtcNow,
                    Read = false
                };

                var commRepo = new CommunicationRepo(_connectionString);
                commRepo.AddCom(communication);
                Console.WriteLine($"💾 Saved driver message to database with ID: {communication.Id}");

                // Get driver name - use provided name or look up from database
                var driverName = message.DriverName;
                if (string.IsNullOrEmpty(driverName))
                {
                    var userRepo = new UserRepo(_connectionString);
                    var driver = userRepo.GetDriverById(message.DriverId);
                    driverName = driver?.Name ?? $"Driver #{message.DriverId}";
                    Console.WriteLine($"📛 Looked up driver name from database: {driverName}");
                }

                // Send to all connected dispatchers with driver name for display
                var dispatcherConnections = _dispatcherConnections.Values.ToList();
                if (dispatcherConnections.Any())
                {
                    // Create a message object with driver name for the notification panel
                    var messageForDispatchers = new
                    {
                        communication.Id,
                        communication.Message,
                        communication.DriverId,
                        communication.From,
                        communication.Date,
                        communication.Read,
                        DriverName = driverName // Include driver name (from request or database lookup)
                    };
                    await Clients.Clients(dispatcherConnections).SendAsync("ReceiveMessage", messageForDispatchers);

                    Console.WriteLine($"Message sent from driver {message.DriverId} ({driverName}) to all dispatchers: {message.Message}");
                }
                else
                {
                    Console.WriteLine("No dispatchers connected to receive message from driver - message saved to database only");
                }

                // Return the communication object with its database ID
                return communication;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message from driver to dispatchers: {ex.Message}");
                throw;
            }
        }

        // Socket for when a dispatcher sends a broadcast message to all drivers
        public async Task DispatcherBroadcastMessage(int dispatcherId, string messageText)
        {
            try
            {
                // Get all drivers
                var userRepo = new UserRepo(_connectionString);
                var activeDrivers = userRepo.GetAllDrivers();

                if (!activeDrivers.Any())
                {
                    Console.WriteLine("No  drivers to broadcast message to");
                    return;
                }

                var commRepo = new CommunicationRepo(_connectionString);
                var broadcastMessage = $"[BROADCAST] {messageText}";
                var timestamp = DateTime.UtcNow;

                // Save a Communication record for each active driver
                foreach (var driver in activeDrivers)
                {
                    var communication = new Communication
                    {
                        Message = broadcastMessage,
                        DriverId = driver.Id,
                        From = "Broadcast",
                        Date = timestamp,
                        Read = false
                    };
                    commRepo.AddCom(communication);

                    var pushToken = userRepo.GetDriverPushToken(driver.Id);
                    if (!string.IsNullOrEmpty(pushToken))
                    {
                        // Truncate message for notification preview (max 100 chars)
                        var previewMessage = communication.Message.Length > 100
                            ? communication.Message.Substring(0, 97) + "..."
                            : communication.Message;

                        await _pushNotificationService.SendPushNotificationAsync(
                            pushToken,
                            "💬 New Message from Dispatch",
                            previewMessage,
                            new
                            {
                                type = "NEW_MESSAGE",
                                messageId = communication.Id,
                                screen = "messages"
                            },
                            driver.Id,
                            "BROADCAST_MESSAGE"
                        );
                        Console.WriteLine($"Push notification sent for message to driver {communication.DriverId}");
                    }
                }

                // Send to all connected drivers
                var driverConnectionIds = new List<string>();
                foreach (var driver in activeDrivers)
                {
                    if (_driverConnections.TryGetValue(driver.Id.ToString(), out var connectionId))
                    {
                        driverConnectionIds.Add(connectionId);
                    }
                }

                if (driverConnectionIds.Any())
                {
                    await Clients.Clients(driverConnectionIds).SendAsync("ReceiveMessage", new
                    {
                        messageId = 0, // Broadcast doesn't have a single ID
                        message = broadcastMessage,
                        from = "Broadcast",
                        fromDispatcherId = dispatcherId,
                        timestamp = timestamp,
                        read = false,
                        isBroadcast = true
                    });

                    Console.WriteLine($"Broadcast message sent from dispatcher {dispatcherId} to {driverConnectionIds.Count} drivers: {messageText}");
                }

                // Notify other dispatchers about the broadcast
                var otherDispatchers = _dispatcherConnections.Values
                    .Where(conn => conn != Context.ConnectionId).ToList();

                if (otherDispatchers.Any())
                {
                    await Clients.Clients(otherDispatchers).SendAsync("BroadcastSent", new
                    {
                        message = broadcastMessage,
                        fromDispatcherId = dispatcherId,
                        recipientCount = activeDrivers.Count,
                        timestamp = timestamp
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting message to drivers: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Legacy Messaging (keeping for backward compatibility)

        public async Task SendMessageToDriver(MessageData message)
        {
            if (string.IsNullOrEmpty(message.DriverId))
            {
                Console.WriteLine("Error: DriverId is required for SendMessageToDriver");
                return;
            }

            if (_driverConnections.TryGetValue(message.DriverId, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", new
                {
                    driverId = message.DriverId,
                    message = message.Message,
                    rideId = message.RideId,
                    timestamp = message.Timestamp ?? DateTime.Now.ToString("o"),
                    fromDriverId = (string?)null
                });

                Console.WriteLine($"Message sent to driver {message.DriverId}: {message.Message}");
            }
            else
            {
                Console.WriteLine($"Driver {message.DriverId} not connected");
            }
        }

        public async Task SendMessageToDispatcher(MessageData message)
        {
            if (string.IsNullOrEmpty(message.FromDriverId))
            {
                Console.WriteLine("Error: FromDriverId is required for SendMessageToDispatcher");
                return;
            }

            var dispatcherConnections = _dispatcherConnections.Values.ToList();
            if (dispatcherConnections.Any())
            {
                await Clients.Clients(dispatcherConnections).SendAsync("ReceiveMessage", new
                {
                    fromDriverId = message.FromDriverId,
                    message = message.Message,
                    rideId = message.RideId,
                    timestamp = message.Timestamp ?? DateTime.Now.ToString("o"),
                    driverId = (string?)null
                });

                Console.WriteLine($"Message sent to dispatchers from driver {message.FromDriverId}: {message.Message}");
            }
            else
            {
                Console.WriteLine("No dispatchers connected to receive message");
            }
        }

        #endregion

        #region Message Read Status

        /// <summary>
        /// Mark message(s) as read in database and notify sender
        /// Can be called by either driver or dispatcher when they view a message
        /// </summary>
        public async Task MarkMessagesAsRead(int[] messageIds, string markedByType)
        {
            try
            {
                if (messageIds == null || messageIds.Length == 0)
                {
                    Console.WriteLine("No message IDs provided to mark as read");
                    return;
                }

                Console.WriteLine($"Marking {messageIds.Length} messages as read by {markedByType}");

                var commRepo = new CommunicationRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);

                foreach (var messageId in messageIds)
                {
                    // Mark as read in database
                    commRepo.MarkAsRead(messageId);
                    Console.WriteLine($"📝 Marked message {messageId} as read in database");

                    // Get the message details to determine who to notify
                    var message = commRepo.GetById(messageId);

                    if (message != null)
                    {
                        Console.WriteLine($"📨 Message {messageId}: From={message.From}, DriverId={message.DriverId}, MarkedBy={markedByType}");
                        var isDriverMessage = message.From.ToLower().Contains("driver");

                        // If it's a driver message (read by dispatcher), notify the driver
                        if (isDriverMessage && markedByType.ToLower() == "dispatcher")
                        {
                            Console.WriteLine($"🔔 Attempting to notify driver {message.DriverId} that their message was read");
                            // Get the driver's connection ID
                            if (_driverConnections.TryGetValue(message.DriverId.ToString(), out var driverConnectionId))
                            {
                                await Clients.Client(driverConnectionId).SendAsync("MessageMarkedAsRead", new
                                {
                                    messageId = messageId,
                                    markedBy = "dispatcher",
                                    timestamp = DateTime.UtcNow
                                });
                                Console.WriteLine($"✅ Notified driver {message.DriverId} that message {messageId} was read");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ Driver {message.DriverId} not connected - cannot send read receipt");
                            }
                        }
                        // If it's a dispatcher/broadcast message (read by driver), notify all dispatchers
                        else if (!isDriverMessage && markedByType.ToLower() == "driver")
                        {
                            Console.WriteLine($"🔔 Attempting to notify dispatchers that driver {message.DriverId} read message {messageId}");
                            Console.WriteLine($"📊 Current _dispatcherConnections count: {_dispatcherConnections.Count}");
                            Console.WriteLine($"📋 Dispatcher connections in dictionary: {string.Join(", ", _dispatcherConnections.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

                            var dispatcherConnections = _dispatcherConnections.Values.ToList();
                            Console.WriteLine($"📡 Found {dispatcherConnections.Count} connected dispatchers");
                            Console.WriteLine($"📡 Connection IDs to notify: {string.Join(", ", dispatcherConnections)}");

                            if (dispatcherConnections.Any())
                            {
                                await Clients.Clients(dispatcherConnections).SendAsync("MessageMarkedAsRead", new
                                {
                                    messageId = messageId,
                                    driverId = message.DriverId,
                                    markedBy = "driver",
                                    timestamp = DateTime.UtcNow
                                });
                                Console.WriteLine($"✅ Notified {dispatcherConnections.Count} dispatchers that driver {message.DriverId} read message {messageId}");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ No dispatchers connected - cannot send read receipt");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Message type/marker mismatch - not sending notification (isDriverMessage={isDriverMessage}, markedByType={markedByType})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Message {messageId} not found in database");
                    }
                }

                Console.WriteLine($"Successfully marked {messageIds.Length} messages as read");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking messages as read: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        public async Task<object> GetConnectedDrivers()
        {
            var drivers = _driverStatuses.Values.Select(d => new
            {
                driverId = d.DriverId,
                status = d.Status,
                lastUpdate = d.LastUpdate,
                isConnected = _driverConnections.ContainsKey(d.DriverId)
            }).ToList();

            await Task.CompletedTask;
            return drivers;
        }

        public async Task<object> GetConnectionStats()
        {
            var stats = new
            {
                connectedDrivers = _driverConnections.Count,
                connectedDispatchers = _dispatcherConnections.Count,
                availableDrivers = _driverStatuses.Count(kvp => kvp.Value.Status == "available"),
                onCallDrivers = _driverStatuses.Count(kvp => kvp.Value.Status == "on-call"),
                totalConnections = Clients.All != null ? 1 : 0
            };

            await Task.CompletedTask;
            return stats;
        }

        #endregion
    }

    #region Data Models

    public class MessageData
    {
        public string? DriverId { get; set; }
        public string? FromDriverId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? RideId { get; set; }
        public string? Timestamp { get; set; }
    }

    public class DispatcherMessageData
    {
        public int DispatcherId { get; set; }
        public int DriverId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? RideId { get; set; }
    }

    public class DriverMessageData
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? RideId { get; set; }
    }

    public class CallUpdate
    {
        public int CallId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AssignedDriverId { get; set; }
        public string? Timestamp { get; set; }
    }

    public class DriverStatusUpdate
    {
        public string DriverId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "available", "on-call", "offline"
        public string Timestamp { get; set; } = string.Empty;
    }

    public class DriverStatus
    {
        public string DriverId { get; set; } = string.Empty;
        public string Status { get; set; } = "available";
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Stores real-time driver location data for map tracking
    /// </summary>
    public class DriverLocation
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime LastUpdate { get; set; }
        public int? CurrentRideId { get; set; }
        public string? CurrentPickup { get; set; }
        public string? CurrentDropoff { get; set; }
        public string? CustomerName { get; set; }
    }

    #endregion
}
