using DispatchApp.Server.Data.DataRepositories;
using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.Services;
using DispatchApp.Server.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DispatchApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints
    public class RideController : ControllerBase
    {
        private IWebHostEnvironment _webHostEnvironment;

        private readonly string _connectionString;
        private readonly IConfiguration _configuration;

        public RideController(IWebHostEnvironment webHostEnvironment, IConfiguration config)
        {
            _webHostEnvironment = webHostEnvironment;
            _connectionString = config.GetConnectionString("ConStr");
            _configuration = config;
        }

        #region add/edit

        [HttpPost("AddRide")]
        public void AddRide([FromBody] Ride ride)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.AddRide(ride);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("AssignRide")]
        public void AssignRide([FromBody] Assignment assignment)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.AssignRide(assignment.RideId, assignment.AssignToId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("CancelDriver")]
        public void CancelDriver([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.CancelDriver(rideId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("CancelRide")]
        public void CancelRide([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.CancelRide(rideId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("CancelRecurring")]
        public void CancelRecurring([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.CancelRecurring(rideId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("PickUp")]
        public void PickUp([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.MarkRidePickedUp(rideId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("DroppedOff")]
        public void DroppedOff([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.MarkRideDroppedOff(rideId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("AddStop")]
        public void AddStop([FromBody] AddStop stop)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.AddStops(stop.RideId, stop.Address);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("UpdatePrice")]
        public void UpdatePrice([FromBody] ChangePriceDTO newCost)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.ChangePrice(newCost.RideId, newCost.Amount, newCost.DriversComp);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("AddWaitTime")]
        public void AddWaitTime([FromBody] AddTipOrWaitTime waitTimeAmount)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.AddWaitTimeAmount(waitTimeAmount.RideId, waitTimeAmount.Amount);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("AddTip")]
        public void AddTip([FromBody] AddTipOrWaitTime tip)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.AddTip(tip.RideId, tip.Amount);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("ResetPickupTime")]
        public void ResetPickupTime([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.ResetPickupTime(rideId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region Dashboard Metrics

        [HttpGet("Dashboard")]
        public IActionResult GetDashboardMetrics()
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);

                var metrics = new DashboardMetrics
                {
                    AssignedRides = rideRepo.GetAssignedRides(),
                    OpenRides = rideRepo.GetOpenRidesNotAssigned(),
                    RidesInProgress = rideRepo.GetRidesInProgress(),
                    RecurringRidesThisWeek = rideRepo.GetRecurringRidesThisWeek(),
                    TodaysRides = rideRepo.GetTodaysRides(),
                    ActiveDrivers = userRepo.GetActiveDrivers(),
                    DriversOnJob = userRepo.GetDriversOnJob()
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/AssignedRides")]
        public IActionResult GetDashboardAssignedRides()
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var rides = rideRepo.GetAssignedRides();
                return Ok(rides);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/OpenRides")]
        public IActionResult GetDashboardOpenRides()
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var rides = rideRepo.GetOpenRidesNotAssigned();
                return Ok(rides);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/RidesInProgress")]
        public IActionResult GetDashboardRidesInProgress()
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var rides = rideRepo.GetRidesInProgress();
                return Ok(rides);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/RecurringRidesThisWeek")]
        public IActionResult GetDashboardRecurringRidesThisWeek()
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var rides = rideRepo.GetRecurringRidesThisWeek();
                return Ok(rides);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Dashboard/TodaysRides")]
        public IActionResult GetDashboardTodaysRides()
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var rides = rideRepo.GetTodaysRides();
                return Ok(rides);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion

        #region Get rides

        [HttpGet("Open")]
        public IActionResult GetOpenRides()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var open = rides.GetOpenRidesNotAssigned();

                if (open.Any())
                {
                    return Ok(open);
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

        [HttpGet("Open/{userId}")]
        public IActionResult GetOpenRides(int userId)
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var open = rides.GetOpenRidesNotAssigned(userId);

                if (!open.Any())
                {
                    return NoContent();
                }

                // Filter out rides that the driver cannot take due to schedule conflicts
                var availabilityService = new DriverAvailabilityService(_connectionString, _configuration);
                var availableRides = open.Where(ride => availabilityService.IsDriverAvailableForCall(userId, ride)).ToList();

                Console.WriteLine($"GetOpenRides for driver {userId}: {open.Count} total open rides, {availableRides.Count} available after schedule check");

                if (availableRides.Any())
                {
                    return Ok(availableRides);
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

        [HttpGet("AssignedRides")]
        public IActionResult GetAssignedRides()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var assigned = rides.GetAssignedRides();

                if (assigned.Any())
                {
                    return Ok(assigned);
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

        [HttpGet("CurrentlyBeingDriven")]
        public IActionResult GetCurrentlyBeingDriven()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var beingDriven = rides.GetRidesCurrentlyBeingDriven();

                if (beingDriven.Any())
                {
                    return Ok(beingDriven);
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

        [HttpGet("GetById")]
        public IActionResult GetById(int id)
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var ride = rides.GetById(id);

                if (ride != null)
                {
                    return Ok(ride);
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

        [HttpGet("FutureRides")]
        public IActionResult GetFutureRides()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var future = rides.GetFutureRides();

                if (future.Any())
                {
                    return Ok(future);
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

        [HttpGet("TodaysRides")]
        public IActionResult GetTodaysRides()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var today = rides.GetTodaysRides();

                if (today.Any())
                {
                    return Ok(today);
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

        [HttpGet("RidesThisWeek")]
        public IActionResult RidesThisWeek()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var today = rides.GetAllRidesThisWeek();

                if (today.Any())
                {
                    return Ok(today);
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

        [HttpGet("AllRides")]
        public IActionResult AllRides()
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var today = rides.GetAllRides();

                if (today.Any())
                {
                    return Ok(today);
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

        [HttpGet("DriverRideHistory")]
        public IActionResult GetRidesByDriver(int driverId)
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var completed = rides.GetCompletedRides(driverId);

                if (completed.Any())
                {
                    return Ok(completed);
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

        [HttpGet("AssignedRidesByDriver")]
        public IActionResult GetAssignedRidesByDriver(int driverId)
        {
            try
            {
                var rides = new RideRepo(_connectionString);
                var assigned = rides.GetUpcomingRidesByDriver(driverId);

                if (assigned.Any())
                {
                    return Ok(assigned);
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

        #endregion

        #region Distance Calculation

        /// <summary>
        /// Calculate distance and travel time from driver's current location to a destination address.
        /// This proxies the Google Maps Distance Matrix API call through the server to avoid CORS issues
        /// and keep the API key secure.
        /// </summary>
        [HttpPost("CalculateDistance")]
        public async Task<IActionResult> CalculateDistance([FromBody] DistanceRequest request)
        {
            try
            {
                if (request.OriginLatitude == 0 || request.OriginLongitude == 0)
                {
                    return BadRequest(new { error = "Missing origin coordinates" });
                }

                if (string.IsNullOrWhiteSpace(request.DestinationAddress))
                {
                    return BadRequest(new { error = "Missing destination address" });
                }

                var apiKey = _configuration["GoogleMaps:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    return StatusCode(500, new { error = "Google Maps API key not configured" });
                }

                // Convert lat/lng to a string format for the API
                var originStr = $"{request.OriginLatitude},{request.OriginLongitude}";

                var googleMapsService = new GoogleMapsService(apiKey);
                var response = await googleMapsService.GetDistanceAsync(originStr, request.DestinationAddress);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new { error = "Google Maps API call failed" });
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<DistanceMatrixResponse>(content,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status != "OK")
                {
                    Console.WriteLine($"Google Maps API error: {result?.Status} - {result?.Error_message}");
                    return Ok(new
                    {
                        distance = (string)null,
                        duration = (string)null,
                        error = result?.Status ?? "Unknown error",
                        errorMessage = result?.Error_message
                    });
                }

                var element = result.Rows?[0]?.Elements?[0];
                if (element?.Status != "OK")
                {
                    return Ok(new
                    {
                        distance = (string)null,
                        duration = (string)null,
                        error = element?.Status ?? "No route found"
                    });
                }

                return Ok(new
                {
                    distance = element.Distance?.Text,
                    duration = element.Duration?.Text,
                    distanceValue = element.Distance?.Value,
                    durationValue = element.Duration?.Value,
                    error = (string)null
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CalculateDistance error: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class DistanceRequest
        {
            public double OriginLatitude { get; set; }
            public double OriginLongitude { get; set; }
            public string DestinationAddress { get; set; }
        }

        #endregion

        #region Pricing

        [HttpPost("CalculatePrice")]
        public async Task<IActionResult> CalculatePrice([FromBody] CalculatePriceRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Pickup) || string.IsNullOrWhiteSpace(request.DropOff))
                {
                    return BadRequest(new { error = "Pickup and DropOff addresses are required" });
                }

                // Get Google Maps API key from configuration
                var googleApiKey = _configuration["GoogleMaps:ApiKey"];
                if (string.IsNullOrWhiteSpace(googleApiKey))
                {
                    return StatusCode(500, new { error = "Google Maps API key not configured" });
                }

                // Create services
                var googleMapsService = new GoogleMapsService(googleApiKey);
                var pricingService = new PricingService(googleMapsService);

                // Calculate price
                var carType = (CarType)request.CarType;
                var result = await pricingService.CalculatePriceAsync(
                    request.Pickup,
                    request.DropOff,
                    request.Stops ?? new List<string>(),
                    carType,
                    request.IsRoundTrip,
                    request.ScheduledTime
                );

                if (!result.Success)
                {
                    return Ok(new
                    {
                        success = false,
                        error = result.Error
                    });
                }

                return Ok(new
                {
                    success = true,
                    originArea = result.OriginArea,
                    destinationArea = result.DestinationArea,
                    pricingMethod = result.PricingMethod,
                    basePrice = result.BasePrice,
                    rushHourSurcharge = result.RushHourSurcharge,
                    totalPrice = result.TotalPrice,
                    ownerCut = result.OwnerCut,
                    driversCompensation = result.DriversCompensation,
                    hourlyRate = result.HourlyRate,
                    tripHours = result.TripHours,
                    tollEstimate = result.TollEstimate,
                    estimatedDurationMinutes = result.EstimatedDurationMinutes,
                    minimumFareApplied = result.MinimumFareApplied,
                    originalPrice = result.OriginalPrice
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CalculatePrice error: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

    }
}