using DispatchApp.Server.Data.DataRepositories;
using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.Utils;
using DispatchApp.Server.Hubs;
using DispatchApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace DispatchApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints except those marked with [AllowAnonymous]
    public class UserController : ControllerBase
    {
        private IWebHostEnvironment _webHostEnvironment;
        private IConfiguration _configuration;
        private string _connectionString;
        private readonly IHubContext<Dispatch> _hubContext;
        private readonly EmailService _emailService;
        private readonly JwtService _jwtService;

        public UserController(IWebHostEnvironment webHostEnvironment, IConfiguration config, IHubContext<Dispatch> hubContext, JwtService jwtService)
        {
            _webHostEnvironment = webHostEnvironment;
            _configuration = config;
            _connectionString = config.GetConnectionString("ConStr");
            _hubContext = hubContext;
            _emailService = new EmailService(config);
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        [AllowAnonymous] // Allow unauthenticated access to login
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                Dispatcher dispatcher = null;
                Driver driver = null;

                if (request.UserType.ToLower() == "dispatcher")
                    dispatcher = user.GetDispatcherByNameOrEmail(request.NameOrEmail);
                else if (request.UserType.ToLower() == "driver")
                    driver = user.GetDriverByNameOrEmail(request.NameOrEmail);

                var storedHash = dispatcher == null ? driver == null ? null : driver.Password : dispatcher.Password;

                if (storedHash != null && storedHash.Length > 0 && PasswordService.VerifyPassword(request.Password, storedHash))
                {
                    // Check if worker has been fired (EndDate is set)
                    var isFired = false;
                    if (dispatcher != null && dispatcher.EndDate != null)
                        isFired = true;
                    if (driver != null && driver.EndDate != null)
                        isFired = true;

                    if (isFired)
                        return Unauthorized("This account has been deactivated. Please contact your administrator.");

                    // Generate JWT token
                    var userId = dispatcher?.Id ?? driver.Id;
                    var name = dispatcher?.Name ?? driver.Name;
                    var userType = dispatcher != null ? "dispatcher" : "driver";
                    var isAdmin = dispatcher?.IsAdmin ?? false;

                    var token = _jwtService.GenerateToken(userId, name, userType, isAdmin);

                    // Log the login
                    if (dispatcher != null)
                        user.AddLoginEntry(dispatcher.GetType().Name.ToString(), dispatcher.Id);
                    else if (driver != null)
                        user.AddLoginEntry(driver.GetType().Name.ToString(), driver.Id);

                    // Return token and user details
                    var response = new LoginResponse
                    {
                        Token = token,
                        UserId = userId,
                        Name = name,
                        UserType = userType,
                        IsAdmin = isAdmin,
                        UserDetails = dispatcher == null ? (object)driver : (object)dispatcher
                    };

                    return Ok(response);
                }
                else
                {
                    return Unauthorized("Incorrect username or password");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous] // Allow expired tokens to be refreshed
        public IActionResult RefreshToken()
        {
            try
            {
                // Get the token from Authorization header and validate it (allowing expired)
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("No token provided");
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // Validate the token but allow expired tokens
                var principal = _jwtService.ValidateTokenAllowExpired(token);
                if (principal == null)
                {
                    return Unauthorized("Invalid token");
                }

                // Get the user's claims from the validated token
                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                var nameClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.Name);
                var userTypeClaim = principal.FindFirst("userType");
                var roleClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.Role);

                if (userIdClaim == null || nameClaim == null || userTypeClaim == null)
                {
                    return Unauthorized("Invalid token");
                }

                // Generate a new token with the same claims
                var userId = int.Parse(userIdClaim.Value);
                var name = nameClaim.Value;
                var userType = userTypeClaim.Value;
                var isAdmin = roleClaim?.Value == "Admin";

                var newToken = _jwtService.GenerateToken(userId, name, userType, isAdmin);

                // Get fresh user details
                var user = new UserRepo(_connectionString);
                object userDetails = null;

                if (userType == "dispatcher")
                {
                    userDetails = user.GetDispatcherById(userId);
                }
                else if (userType == "driver")
                {
                    userDetails = user.GetDriverById(userId);
                }

                var response = new LoginResponse
                {
                    Token = newToken,
                    UserId = userId,
                    Name = name,
                    UserType = userType,
                    IsAdmin = isAdmin,
                    UserDetails = userDetails
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return Unauthorized("Token refresh failed: " + ex.Message);
            }
        }

        [HttpPost("logout")]
        public void Logout([FromBody] LogoutRequest request)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                user.Logout(request.UserType, request.UserId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        #region add/edit
        [HttpPost("AddDispatcher")]
        public async Task<IActionResult> NewDispatcher([FromBody] Dispatcher dispatcher)
        {
            try
            {
                var user = new UserRepo(_connectionString);

                dispatcher.UserName = user.CreateUserName(dispatcher.Name);


                // Generate a random password
                var generatedPassword = PasswordService.GeneratePassword();

                dispatcher.DateJoined = DateTime.UtcNow;
                dispatcher.Password = PasswordService.HashPassword(generatedPassword);

                user.AddDispatcher(dispatcher);

                // Send password via email
                var emailSent = await _emailService.SendPasswordEmail(
                    dispatcher.Email,
                    dispatcher.Name,
                    generatedPassword,
                    "dispatcher",
                    dispatcher.UserName
                );

                if (!emailSent)
                {
                    return StatusCode(207, new
                    {
                        success = true,
                        warning = "Dispatcher created successfully, but failed to send password email. Please contact the dispatcher directly with their temporary password.",
                        tempPassword = generatedPassword
                    });
                }

                return Ok(new { success = true, message = "Dispatcher created and password sent via email." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("AddDriver")]
        public async Task<IActionResult> NewDriver([FromBody] Driver driver)
        {
            try
            {
                var user = new UserRepo(_connectionString);

                driver.UserName = user.CreateUserName(driver.Name);

                // Generate a random password
                var generatedPassword = PasswordService.GeneratePassword();

                driver.JoinedDate = DateTime.UtcNow;
                driver.Password = PasswordService.HashPassword(generatedPassword);

                user.AddDriver(driver);

                // Send password via email
                var emailSent = await _emailService.SendPasswordEmail(
                    driver.Email,
                    driver.Name,
                    generatedPassword,
                    "driver",
                    driver.UserName
                );

                if (!emailSent)
                {
                    return StatusCode(207, new
                    {
                        success = true,
                        warning = "Driver created successfully, but failed to send password email. Please contact the driver directly with their temporary password.",
                        tempPassword = generatedPassword
                    });
                }

                return Ok(new { success = true, message = "Driver created and password sent via email." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("UpdateDispatcher")]
        public void UpdateDispatcher([FromBody] Dispatcher dispatcher)
        {
            try
            {
                var user = new UserRepo(_connectionString);

                user.UpdateDispatcher(dispatcher);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("UpdateDriver")]
        public void UpdateDriver([FromBody] Driver driver)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                user.UpdateDriver(driver);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("FireDispatcher")]
        public void FireDispatcher([FromBody] int dispatcherId)
        {
            try
            {
                var user = new UserRepo(_connectionString);

                user.FireDispathcer(dispatcherId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("FireDriver")]
        public async Task<IActionResult> FireDriver([FromBody] int driverId)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var rideRepo = new RideRepo(_connectionString);

                // Fire the driver and get list of affected ride IDs
                var affectedRideIds = user.FireDriver(driverId);

                // Notify all drivers about newly available calls
                if (affectedRideIds.Any())
                {
                    foreach (var rideId in affectedRideIds)
                    {
                        var rideData = rideRepo.GetById(rideId);
                        if (rideData != null)
                        {
                            // Broadcast to all drivers that this call is now available
                            await _hubContext.Clients.All.SendAsync("CallAvailableAgain", new
                            {
                                rideId = rideId,
                                ride = rideData,
                                message = "A driver was removed from this call. It's now available.",
                                timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }

                return Ok(new
                {
                    message = "Driver fired successfully",
                    affectedCalls = affectedRideIds.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("AddCar")]
        public void NewCar([FromBody] Car car)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                user.AddCar(car);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("UpdateCar")]
        public void UpdateCar([FromBody] Car car)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                user.UpdateCar(car);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("SetPrimaryCar")]
        public IActionResult SetPrimaryCar([FromBody] SetPrimaryCarRequest car)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                user.SetPrimaryCar(car.CarId);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion

        #region get

        [HttpGet("ActiveDispatchers")]
        public IActionResult GetActiveDispatchers()
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var activeDispatchers = user.GetActiveDispatchers();

                if (activeDispatchers != null && activeDispatchers.Any())
                {
                    return Ok(activeDispatchers);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("LoggedInDispatchers")]
        public IActionResult GetLogedInDispatchers()
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var loggedInDispatchers = user.GetLoggedInDispatchers();

                if (loggedInDispatchers != null && loggedInDispatchers.Any())
                {
                    return Ok(loggedInDispatchers);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("ActiveDrivers")]
        public IActionResult GetActiveDrivers()
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var activeDrivers = user.GetActiveDrivers();

                if (activeDrivers != null && activeDrivers.Any())
                {
                    return Ok(activeDrivers);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/ActiveDrivers")]
        public IActionResult GetDashboardActiveDrivers()
        {
            try
            {
                var userRepo = new UserRepo(_connectionString);
                var drivers = userRepo.GetActiveDrivers();
                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/DriversOnJob")]
        public IActionResult GetDashboardDriversOnJob()
        {
            try
            {
                var userRepo = new UserRepo(_connectionString);
                var drivers = userRepo.GetDriversOnJob();
                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("AllDrivers")]
        public IActionResult GetAllDrivers()
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var activeDrivers = user.GetAllDrivers();

                if (activeDrivers != null && activeDrivers.Any())
                {
                    return Ok(activeDrivers);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("ActiveDriversOnCall")]
        public IActionResult GetActiveDriversOnCall()
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var busyDrivers = user.GetAllBusyDrivers();

                if (busyDrivers != null && busyDrivers.Any())
                {
                    return Ok(busyDrivers);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("DriverById")]
        public IActionResult GetDriverById(int id)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var driver = user.GetDriverById(id);

                if (driver != null)
                {
                    return Ok(driver);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("isAdmin")]
        public IActionResult IsAdmin(int userId)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                return Ok(user.IsAdmin(userId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("getCars")]
        public IActionResult GetCars(int userId)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var cars = user.GetCars(userId);

                if (cars != null && cars.Any())
                {
                    return Ok(cars);
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("getDriverStatus")]
        public IActionResult GetDriverStatus(int userId)
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var status = user.GetDriverStatus(userId);
                return Ok(user.GetDriverStatus(userId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("GetFiredWorkers")]
        public IActionResult GetFiredWorkers()
        {
            try
            {
                var user = new UserRepo(_connectionString);
                var dispatchers = user.GetFiredDispatchers();
                var drivers = user.GetFiredDrivers();

                if ((dispatchers != null && dispatchers.Any()) || (drivers != null && drivers.Any()))
                {
                    return Ok(new
                    {
                        Dispatchers = dispatchers,
                        Drivers = drivers
                    });
                }
                else
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("getDriversDriving")]
        public IActionResult GetDriversDriving()
        {
            try
            {
                var userRepo = new UserRepo(_connectionString);
                var rideRepo = new RideRepo(_connectionString);

                // Get all rides currently being driven (assigned, scheduled time passed or picked up, not dropped off)
                var activeRides = rideRepo.GetRidesCurrentlyBeingDriven();

                // Build response with driver info and their current ride using DriverRideShare
                var driversDriving = activeRides
                    .Where(ride => (ride.Reassigned && ride.ReassignedToId.HasValue) || ride.AssignedToId.HasValue)
                    .Select(ride =>
                    {
                        var driver = ride.AssignedTo ?? userRepo.GetDriverById(ride.AssignedToId.Value);
                        if (driver == null) return null;

                        // Get upcoming rides for this driver
                        var upcomingRides = rideRepo.GetUpcomingRidesByDriver(driver.Id)
                            .OrderBy(r => r.ScheduledFor)
                            .Take(5)
                            .ToList();

                        return new DriverRideShare
                        {
                            Driver = driver,
                            PrimaryCar = userRepo.GetPrimaryCar(driver.Id),
                            CurrentRide = ride,
                            UpcomingRides = upcomingRides,
                            IsOnline = Hubs.Dispatch.IsDriverOnline(driver.Id)
                        };
                    })
                    .Where(d => d != null)
                    .ToList();

                return Ok(driversDriving);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("getOnlineDrivers")]
        public IActionResult GetOnlineDrivers()
        {
            try
            {
                var userRepo = new UserRepo(_connectionString);
                var rideRepo = new RideRepo(_connectionString);

                // Get online driver IDs from SignalR hub
                var onlineDriverIds = Hubs.Dispatch.GetOnlineDriverIds();

                // Get drivers who are currently driving (have active rides)
                var activeDrivers = userRepo.GetAllBusyDrivers();

                // Filter to only online drivers who are NOT currently driving
                var availableDriverIds = onlineDriverIds.Where(id => !activeDrivers.Any(x => x.Id == id)).ToList();

                // Build response with driver info and upcoming rides
                var onlineDrivers = availableDriverIds.Select(driverId =>
                {
                    var driver = userRepo.GetDriverById(driverId);
                    if (driver == null) return null;

                    var primaryCar = userRepo.GetPrimaryCar(driverId);

                    // Get upcoming rides for this driver (assigned but not yet started)
                    var upcomingRides = rideRepo.GetUpcomingRidesByDriver(driverId)
                        .OrderBy(r => r.ScheduledFor)
                        .Take(5) // Limit to next 5 rides
                        .ToList();

                    return new DriverRideShare
                    {
                        Driver = driver,
                        PrimaryCar = userRepo.GetPrimaryCar(driver.Id),
                        UpcomingRides = upcomingRides,
                        IsOnline = Hubs.Dispatch.IsDriverOnline(driver.Id)
                    };
                }).Where(d => d != null).ToList();

                return Ok(onlineDrivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion

        #region Push Notifications

        /// <summary>
        /// Registers or updates the Expo Push Token for a driver.
        /// This endpoint is called by the DriverApp when:
        /// 1. The driver logs in
        /// 2. The app restarts and gets a new token
        /// 3. The token is refreshed by Expo
        /// 
        /// The push token is used to send push notifications to the driver's device
        /// for new calls, messages, cancellations, etc.
        /// </summary>
        [HttpPost("RegisterPushToken")]
        public IActionResult RegisterPushToken([FromBody] PushTokenRequest request)
        {
            try
            {
                if (request.DriverId <= 0)
                {
                    return BadRequest("Invalid driver ID");
                }

                if (string.IsNullOrEmpty(request.PushToken))
                {
                    return BadRequest("Push token is required");
                }

                // Validate token format (should start with ExponentPushToken)
                if (!request.PushToken.StartsWith("ExponentPushToken"))
                {
                    return BadRequest("Invalid push token format. Expected ExponentPushToken format.");
                }

                var userRepo = new UserRepo(_connectionString);
                userRepo.UpdateDriverPushToken(request.DriverId, request.PushToken);

                return Ok(new { success = true, message = "Push token registered successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Removes the push token when a driver logs out.
        /// This prevents notifications from being sent to a device after logout.
        /// </summary>
        [HttpPost("UnregisterPushToken")]
        public IActionResult UnregisterPushToken([FromBody] int driverId)
        {
            try
            {
                if (driverId <= 0)
                {
                    return BadRequest("Invalid driver ID");
                }

                var userRepo = new UserRepo(_connectionString);
                userRepo.UpdateDriverPushToken(driverId, null);

                return Ok(new { success = true, message = "Push token removed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        #endregion

        #region password management

        [HttpPost("UpdatePassword")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
        {
            if (request == null || request.UserId == 0 || string.IsNullOrEmpty(request.UserType))
            {
                return BadRequest(new { success = false, message = "Missing required fields" });
            }

            var userRepo = new UserRepo(_connectionString);

            // Verify old password first
            var isValid = await userRepo.VerifyPassword(request.UserId, request.UserType, request.OldPassword);

            if (!isValid)
            {
                return BadRequest(new { success = false, message = "Current password is incorrect" });
            }

            // Update to new password
            var result = await userRepo.UpdatePassword(request.UserId, request.UserType, request.NewPassword);
            if (result)
            {
                return Ok(new { success = true, message = "Password updated successfully" });
            }

            return BadRequest(new { success = false, message = "Failed to update password" });
        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var userRepo = new UserRepo(_connectionString);

            // Get user email
            var email = await userRepo.GetUserEmail(request.UserId, request.UserType);
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new { success = false, message = "User not found or no email on file" });
            }

            // Generate new random password
            var newPassword = PasswordService.GeneratePassword();

            // Update password in database
            var result = await userRepo.UpdatePassword(request.UserId, request.UserType, newPassword);

            if (!result)
            {
                return BadRequest(new { success = false, message = "Failed to reset password" });
            }

            // Send email with new password
            try
            {
                await _emailService.SendPasswordResetEmail(email, newPassword);
                return Ok(new { success = true, message = "New password has been sent to your email" });
            }
            catch (Exception ex)
            {
                // Password was changed but email failed - log this
                return Ok(new { success = true, message = "Password reset successful. Please contact admin for new password." });
            }
        }

        #endregion
    }

    public class PushTokenRequest
    {
        public int DriverId { get; set; }
        public string PushToken { get; set; } = string.Empty;
    }

}
