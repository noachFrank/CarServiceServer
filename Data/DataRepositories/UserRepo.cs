using DispatchApp.Server.data;
using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DispatchApp.Server.Data.DataRepositories
{
    public class UserRepo
    {
        private string _connectionString;

        public UserRepo(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region dispatchers

        public List<Dispatcher> GetAllDispatchers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Dispatchers.ToList();
            }
        }

        public List<Dispatcher> GetActiveDispatchers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Dispatchers.Where(x => x.EndDate == null).ToList();
            }
        }

        public List<Dispatcher> GetLoggedInDispatchers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Dispatchers.Where(x => x.IsActive != null && x.IsActive &&
                           (context.Logins.FirstOrDefault(y => y.WorkerType.ToLower() == "dispatcher" && y.WorkerId == x.Id
                           && y.LoginTime != null && y.LogoutTime == null) != null)).ToList();
            }
        }

        public Dispatcher GetDispatcherByNameOrEmail(string nameOrEmail)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Dispatchers.FirstOrDefault(x => (x.UserName != null && x.UserName.ToLower() == nameOrEmail.ToLower())
                                                            || (x.Name != null && x.Name.ToLower() == nameOrEmail.ToLower())
                                                            || (x.Email != null && x.Email.ToLower() == nameOrEmail.ToLower()));
            }
        }

        public List<Dispatcher> GetFiredDispatchers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Dispatchers.Where(x => x.EndDate != null && x.EndDate <= DateTime.UtcNow).ToList();
            }
        }

        public Dispatcher GetDispatcherById(int id)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var dispatcher = context.Dispatchers.FirstOrDefault(x => x.Id == id);

                return dispatcher;
            }
        }


        #endregion

        #region drivers 


        public List<Driver> GetAllDrivers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers.Include(d => d.Cars).Where(x => x.EndDate == null).ToList();
            }
        }

        public List<Driver> GetFiredDrivers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers.Include(d => d.Cars).Where(x => x.EndDate != null && x.EndDate <= DateTime.UtcNow).ToList();
            }
        }

        public List<Driver> GetActiveDrivers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers.Include(d => d.Cars).Where(x => x.EndDate == null).ToList();
            }
        }

        public List<Driver> GetAvailableDrivers()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers.Include(d => d.Cars).Where(x => x.Active != null && x.Active && (x.OnJob == null || !x.OnJob) && x.EndDate == null).ToList();
            }
        }

        public List<Driver> GetAllBusyDrivers()
        {
            var rideRepo = new RideRepo(_connectionString);
            var activeRides = rideRepo.GetRidesCurrentlyBeingDriven();

            // Extract driver IDs from active rides (handle both assigned and reassigned)
            var busyDriverIds = activeRides
                .SelectMany(r => new[] {
                    r.AssignedToId,
                    r.Reassigned == true ? r.ReassignedToId : null
                })
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();

            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers.Include(d => d.Cars).Where(x => x.EndDate == null && busyDriverIds.Contains(x.Id)).ToList();
            }
        }

        public Driver GetDriverByNameOrEmail(string nameOrEmail)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers.Include(d => d.Cars).FirstOrDefault(x => x.EndDate == null && ((x.UserName != null && x.UserName.ToLower() == nameOrEmail.ToLower())
                                                        || (x.Name != null && x.Name.ToLower() == nameOrEmail.ToLower())
                                                        || (x.Email != null && x.Email.ToLower() == nameOrEmail.ToLower())));
            }
        }

        public Driver GetDriverById(int id)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var driver = context.Drivers.Include(x => x.Cars).FirstOrDefault(x => x.Id == id);

                return driver;
            }
        }

        public List<Car> GetCars(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Cars.Where(x => x.DriverId == driverId).ToList();
            }
        }

        public List<Driver> GetDriversOnJob()
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var activeRides = context.Rides.Where(x => x.DropOffTime == null && (x.PickupTime != null || x.ScheduledFor <= DateTime.UtcNow));
               
                return context.Drivers.Include(d => d.Cars).Where(x => activeRides.Any(y => (y.Reassigned && y.ReassignedToId == x.Id) || (!y.Reassigned && y.AssignedToId == x.Id))).ToList();
            }
        }

        public Car GetPrimaryCar(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Cars.FirstOrDefault(x => x.DriverId == driverId && x.IsPrimary);
            }
        }

        public string GetDriverStatus(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var driver = GetDriverById(driverId);
                var ride = context.Rides.FirstOrDefault(x => ((!x.Reassigned && x.AssignedToId == driverId) || (x.Reassigned && x.ReassignedToId == driverId)) 
                                                             && x.DropOffTime == null && x.ScheduledFor <= DateTime.UtcNow);

                if (driver == null)
                    return "No Driver Found";
                else if (driver.EndDate != null && driver.EndDate?.Date >= DateTime.Today.Date)
                    return "Inactive";
                else if (ride == null)
                    return "Available";
                else if (ride.PickupTime != null)
                    return "Driving";
                else if (ride.PickupTime == null)
                    return "En-Route";
                return "Unknown";
            }
        }



        #endregion


        #region login/logoff
        public void AddLoginEntry(string workerType, int id)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var login = new Login()
                {
                    WorkerType = workerType.ToLower(),
                    WorkerId = id,
                    LoginTime = DateTime.Now
                };
                context.Logins.Add(login);
                context.SaveChanges();
            }
        }

        public void Logout(string workerType, int id)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var login = context.Logins.FirstOrDefault(x => x.WorkerType != null && x.WorkerType.ToLower() == workerType.ToLower() &&
                                                                x.WorkerId != null && x.WorkerId == id && x.LogoutTime == null);
                if (login != null && login.LogoutTime == null)
                {
                    var rideRepo = new RideRepo(_connectionString);
                    var rideCount = 0;

                    switch (workerType.ToLower())
                    {
                        case "driver":
                            rideCount = rideRepo.GetCompletedRides(id, DateTime.Today).Where(x => x.PickupTime >= login.LoginTime).Count();
                            break;
                        case "Dispatcher":
                            rideCount = rideRepo.GetDispatchedRides(id, DateTime.Today).Where(x => x.PickupTime >= login.LoginTime).Count();
                            break;
                    }

                    login.LogoutTime = DateTime.Now;
                    login.CallsTaken = rideCount;
                    context.SaveChanges();
                }
            }
        }

        public bool IsAdmin(int userId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var user = context.Dispatchers.FirstOrDefault(x => x.Id != null && x.Id == userId);
                return user == null || user.IsAdmin == null ? false : user.IsAdmin;
            }
        }


        #endregion

        #region add/edit
        public void AddDispatcher(Dispatcher dispatcher)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                context.Dispatchers.Add(dispatcher);
                context.SaveChanges();
            }
        }

        public void AddDriver(Driver driver)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                context.Drivers.Add(driver);
                context.SaveChanges();
            }
            var notifRepo = new NotificationPreferencesRepo(_connectionString);
            notifRepo.CreateDefaultPreferencesAsync(driver.Id);
        }

        public string CreateUserName(string name)
        {
            var names = name.Split(" ");
            if (names.Length >= 2)
            {
                var userName = names[0].FirstOrDefault() + names[1];

                if (GetDispatcherByNameOrEmail(userName) != null)
                    return string.Join('_', names);
                else
                    return userName;
            }
            else
                return names[0];

        }

        public void AddCar(Car car)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                if (car.IsPrimary)
                {
                    var oldCars = context.Cars.Where(x => x.DriverId == car.DriverId);
                    foreach (var c in oldCars)
                        c.IsPrimary = false;
                }
                context.Cars.Add(car);
                context.SaveChanges();
            }
        }

        public void UpdateDispatcher(Dispatcher dispatcher)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var dsptc = context.Dispatchers.FirstOrDefault(x => x.Id == dispatcher.Id);

                if (dsptc == null)
                    throw new Exception($"Dispatcher with ID {dispatcher.Id} not found.");

                dsptc.IsAdmin = dispatcher.IsAdmin;
                dsptc.IsActive = dispatcher.IsActive;
                dsptc.Name = dispatcher.Name;
                dsptc.PhoneNumber = dispatcher.PhoneNumber;
                dsptc.Email = dispatcher.Email;

                context.SaveChanges();
            }
        }

        public void UpdateDriver(Driver driver)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var drvr = context.Drivers.FirstOrDefault(x => x.Id == driver.Id);

                if (drvr == null)
                    throw new Exception($"driver with ID {driver.Id} not found.");

                drvr.Name = driver.Name;
                drvr.Email = driver.Email;
                drvr.PhoneNumber = driver.PhoneNumber;

                context.SaveChanges();
            }
        }

        public void FireDispathcer(int dispatcherId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var dsptch = context.Dispatchers.FirstOrDefault(x => x.Id == dispatcherId);
                if (dsptch == null)
                    return;
                dsptch.EndDate = DateTime.UtcNow;
                context.SaveChanges();
            }
        }

        public List<int> FireDriver(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var drvr = context.Drivers.FirstOrDefault(x => x.Id == driverId);
                if (drvr == null)
                    return new List<int>();

                drvr.EndDate = DateTime.UtcNow;
                drvr.Active = false;
                drvr.OnJob = false;

                // Find all active (not completed) rides assigned to this driver
                var activeRides = context.Rides
                    .Where(r => r.DropOffTime == null && !r.Canceled &&
                               ((r.Reassigned && r.ReassignedToId == driverId) ||
                                (!r.Reassigned && r.AssignedToId == driverId)))
                    .ToList();

                var affectedRideIds = new List<int>();

                // Mark all active rides as reassigned (unassign the fired driver)
                foreach (var ride in activeRides)
                {
                    ride.Reassigned = true;
                    ride.ReassignedToId = null;
                    affectedRideIds.Add(ride.RideId);
                }

                context.SaveChanges();
                return affectedRideIds;
            }
        }

        public void ChangeDriverActiveStatus(int? driverId)
        {
            if (driverId == null)
                return;
            using (var context = new DispatchDbContext(_connectionString))
            {
                var drvr = context.Drivers.FirstOrDefault(x => x.Id == driverId);

                if (drvr == null)
                    throw new Exception($"driver with ID {driverId} not found.");

                drvr.Active = !drvr.Active;

                context.SaveChanges();
            }
        }

        public void SetDriverActiveStatus(int driverId, bool isActive)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var driver = context.Drivers.FirstOrDefault(x => x.Id == driverId);

                if (driver == null)
                    return;

                driver.Active = isActive;
                context.SaveChanges();
            }
        }

        public void ChangeDriverOnJobStatus(int? driverId, bool onJob)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var drvr = context.Drivers.FirstOrDefault(x => x.Id == driverId);

                if (drvr == null)
                    return;

                drvr.OnJob = onJob;

                context.SaveChanges();
            }
        }

        public void UpdateCar(Car car)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var c = context.Cars.FirstOrDefault(x => x.CarId == car.CarId);

                if (c == null)
                    throw new Exception($"car with ID {car.CarId} not found.");

                c.Type = car.Type;
                c.Make = car.Make;
                c.Model = car.Model;
                c.Year = car.Year;
                c.Seats = car.Seats;
                c.Color = car.Color;
                c.LicensePlate = car.LicensePlate;

                context.SaveChanges();
            }
        }

        public void SetPrimaryCar(int carId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var carToUpdate = context.Cars.FirstOrDefault(x => x.CarId == carId);
                if (carToUpdate == null)
                    throw new Exception($"car with ID {carId} not found.");

                var oldCars = context.Cars.Where(x => x.DriverId == carToUpdate.DriverId);
                //set IsPrimary to be true if the carid matches the passed in one and false if not
                foreach (var car in oldCars)
                    car.IsPrimary = car.CarId == carId;

                context.SaveChanges();
            }
        }

        /// <summary>
        /// Updates the Expo Push Token for a driver.
        /// This token is used to send push notifications to the driver's mobile device.
        /// Called when the driver logs in or when the app restarts and gets a new token.
        /// </summary>
        /// <param name="driverId">The driver's ID</param>
        /// <param name="pushToken">The Expo Push Token (e.g., "ExponentPushToken[xxxxxx]")</param>
        public void UpdateDriverPushToken(int driverId, string? pushToken)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                var driver = context.Drivers.FirstOrDefault(x => x.Id == driverId);

                if (driver == null)
                {
                    Console.WriteLine($"Cannot update push token: Driver {driverId} not found");
                    return;
                }

                driver.ExpoPushToken = pushToken;
                context.SaveChanges();
                Console.WriteLine($"Push token updated for driver {driverId}");
            }
        }

        /// <summary>
        /// Gets the push token for a specific driver.
        /// Returns null if driver not found or has no push token.
        /// </summary>
        public string? GetDriverPushToken(int driverId)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers
                    .Where(x => x.Id == driverId)
                    .Select(x => x.ExpoPushToken)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets push tokens for multiple drivers (for batch notifications).
        /// Returns a dictionary of driverId -> pushToken.
        /// Only includes drivers that have a push token set.
        /// </summary>
        public Dictionary<int, string> GetDriverPushTokens(List<int> driverIds)
        {
            using (var context = new DispatchDbContext(_connectionString))
            {
                return context.Drivers
                    .Where(x => driverIds.Contains(x.Id) && x.ExpoPushToken != null)
                    .ToDictionary(x => x.Id, x => x.ExpoPushToken!);
            }
        }


        #endregion

        #region password management

        public async Task<bool> VerifyPassword(int userId, string userType, string password)
        {
            using var context = new DispatchDbContext(_connectionString);

            switch (userType.ToLower())
            {
                case "dispatcher":
                    var dispatcher = await context.Dispatchers.FirstOrDefaultAsync(x => x.Id == userId);
                    if (dispatcher != null)
                    {
                        Console.WriteLine($"Found dispatcher. Stored hash starts with: {dispatcher.Password?.Substring(0, Math.Min(10, dispatcher.Password?.Length ?? 0))}...");
                        Console.WriteLine($"Stored hash length: {dispatcher.Password?.Length}");
                        Console.WriteLine($"Password provided: '{password}'");
                        Console.WriteLine($"Password length: {password?.Length}");

                        try
                        {
                            var x = PasswordHelper.VerifyPassword(password, dispatcher.Password);
                            Console.WriteLine($"BCrypt verification result: {x}");
                            return x;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"BCrypt verification failed with error: {ex.Message}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Dispatcher with ID {userId} not found");
                    }
                    break;
                case "driver":
                    var driver = await context.Drivers.FirstOrDefaultAsync(x => x.Id == userId);
                    if (driver != null)
                    {
                        Console.WriteLine($"Found driver. Stored hash starts with: {driver.Password?.Substring(0, Math.Min(10, driver.Password?.Length ?? 0))}...");
                        return PasswordHelper.VerifyPassword(password, driver.Password);
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown userType: {userType}");
                    return false;
            }
            return false;
        }

        public async Task<bool> UpdatePassword(int userId, string userType, string newPassword)
        {
            using var context = new DispatchDbContext(_connectionString);

            // Hash the new password
            var hashedPassword = PasswordHelper.HashPassword(newPassword);

            switch (userType.ToLower())
            {
                case "dispatcher":
                    var dispatcher = await context.Dispatchers.FirstOrDefaultAsync(x => x.Id == userId);
                    if (dispatcher != null)
                    {
                        dispatcher.Password = hashedPassword;
                        await context.SaveChangesAsync();
                        return true;
                    }
                    break;
                case "driver":
                    var driver = await context.Drivers.FirstOrDefaultAsync(x => x.Id == userId);
                    if (driver != null)
                    {
                        driver.Password = hashedPassword;
                        await context.SaveChangesAsync();
                        return true;
                    }
                    break;
                default:
                    return false;
            }
            return false;
        }

        public async Task<string?> GetUserEmail(int userId, string userType)
        {
            using var context = new DispatchDbContext(_connectionString);

            switch (userType.ToLower())
            {
                case "dispatcher":
                    var dispatcher = await context.Dispatchers.FirstOrDefaultAsync(x => x.Id == userId);
                    return dispatcher?.Email;
                case "driver":
                    var driver = await context.Drivers.FirstOrDefaultAsync(x => x.Id == userId);
                    return driver?.Email;
                default:
                    return null;
            }
        }

        #endregion
    }
}