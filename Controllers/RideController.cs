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

        [HttpPost("SettleUpDriver")]
        public async Task<IActionResult> SettleUpDriver([FromBody] int driverId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);
                var invoiceRepo = new InvoiceRepo(_connectionString);

                // Get the driver info
                var driver = userRepo.GetDriverById(driverId);
                if (driver == null)
                {
                    return NotFound($"Driver with ID {driverId} not found");
                }

                // Get unsettled rides before settling them
                var unsettledRides = rideRepo.GetUnsettledRidesByDriver(driverId);

                if (unsettledRides == null || unsettledRides.Count == 0)
                {
                    return Ok(new { success = true, message = "No unsettled rides found for this driver" });
                }

                // Generate invoice
                var invoiceService = new InvoiceService();
                var invoiceNumber = InvoiceService.GenerateInvoiceNumber(driverId);
                var pdfBytes = invoiceService.GenerateInvoice(driver, unsettledRides, invoiceNumber);
                Console.WriteLine("invoice generated");

                // Calculate net amount for email
                decimal totalOwedToDriver = 0;
                decimal totalOwedByDriver = 0;
                foreach (var ride in unsettledRides)
                {
                    var driverComp = ride.DriversCompensation ?? 0;
                    if (ride.PaymentType == "cash" || ride.PaymentType == "zelle")
                    {
                        totalOwedByDriver += (ride.Cost - driverComp);
                    }
                    else
                    {
                        totalOwedToDriver += driverComp;
                    }
                }
                var netAmount = totalOwedToDriver - totalOwedByDriver;
                var driverOwesCompany = netAmount < 0;

                var periodStart = unsettledRides.OrderBy(x => x.ScheduledFor).First().ScheduledFor;
                var periodEnd = unsettledRides.OrderBy(x => x.ScheduledFor).Last().ScheduledFor;

                // Save invoice record to database (including PDF data)
                var invoiceRecord = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    DriverId = driverId,
                    DriverName = driver.Name,
                    DriverUsername = driver.UserName,
                    CreatedAt = DateTime.Now,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    RideCount = unsettledRides.Count,
                    TotalOwedToDriver = totalOwedToDriver,
                    TotalOwedByDriver = totalOwedByDriver,
                    NetAmount = netAmount,
                    DriverOwesCompany = driverOwesCompany,
                    PdfData = pdfBytes, // Store PDF in database
                    EmailSent = false
                };

                // Send invoice email (if driver has email)
                if (!string.IsNullOrEmpty(driver.Email))
                {
                    var emailService = new EmailService(_configuration);
                    await emailService.SendDriverInvoiceAsync(
                        driver.Email,
                        driver.Name,
                        pdfBytes,
                        invoiceNumber,
                        Math.Abs(netAmount),
                        driverOwesCompany,
                        periodStart,
                        periodEnd
                    );
                    invoiceRecord.EmailSent = true;
                    invoiceRecord.LastEmailSentAt = DateTime.Now;
                }

                invoiceRepo.AddInvoice(invoiceRecord);

                // Now settle the rides in the database
                rideRepo.SettleDriverRides(driverId);

                return Ok(new
                {
                    success = true,
                    message = "Driver rides settled successfully",
                    invoiceNumber = invoiceNumber,
                    ridesSettled = unsettledRides.Count,
                    emailSent = !string.IsNullOrEmpty(driver.Email)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion

        #region Invoices

        [HttpGet("Invoices/Drivers")]
        public IActionResult GetDriversWithInvoices()
        {
            try
            {
                var invoiceRepo = new InvoiceRepo(_connectionString);
                var drivers = invoiceRepo.GetDriversWithInvoiceCounts();
                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Invoices/Driver/{driverId}")]
        public IActionResult GetDriverInvoices(int driverId)
        {
            try
            {
                var invoiceRepo = new InvoiceRepo(_connectionString);
                var invoices = invoiceRepo.GetInvoicesByDriver(driverId);
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Invoices/Download/{invoiceNumber}")]
        public IActionResult DownloadInvoice(string invoiceNumber)
        {
            try
            {
                var invoiceRepo = new InvoiceRepo(_connectionString);
                var invoice = invoiceRepo.GetByInvoiceNumber(invoiceNumber);

                if (invoice == null)
                {
                    return NotFound("Invoice not found");
                }

                // Get PDF from database
                if (invoice.PdfData == null || invoice.PdfData.Length == 0)
                {
                    return NotFound("Invoice PDF data not found");
                }

                return File(invoice.PdfData, "application/pdf", $"{invoiceNumber}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("Invoices/Resend")]
        public async Task<IActionResult> ResendInvoice([FromBody] ResendInvoiceRequest request)
        {
            try
            {
                var invoiceRepo = new InvoiceRepo(_connectionString);
                var userRepo = new UserRepo(_connectionString);

                var invoice = invoiceRepo.GetByInvoiceNumber(request.InvoiceNumber);
                if (invoice == null)
                {
                    return NotFound("Invoice not found");
                }

                var driver = userRepo.GetDriverById(invoice.DriverId);
                if (driver == null)
                {
                    return NotFound("Driver not found");
                }

                if (string.IsNullOrEmpty(driver.Email))
                {
                    return BadRequest("Driver does not have an email address");
                }

                // Get PDF from database
                if (invoice.PdfData == null || invoice.PdfData.Length == 0)
                {
                    return NotFound("Invoice PDF data not found");
                }

                var emailService = new EmailService(_configuration);
                await emailService.SendDriverInvoiceAsync(
                    driver.Email,
                    driver.Name,
                    invoice.PdfData,
                    invoice.InvoiceNumber,
                    Math.Abs(invoice.NetAmount),
                    invoice.DriverOwesCompany,
                    invoice.PeriodStart,
                    invoice.PeriodEnd
                );

                invoiceRepo.UpdateEmailSent(invoice.InvoiceNumber);

                return Ok(new { success = true, message = "Invoice resent successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        public class ResendInvoiceRequest
        {
            public string InvoiceNumber { get; set; }
        }

        [HttpPost("SettleRide")]
        public IActionResult SettleRide([FromBody] int rideId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                rideRepo.SettleRide(rideId);
                return Ok(new { success = true, message = "Ride settled successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
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
                    DriversOnJob = userRepo.GetDriversOnJob(),
                    UnsettledDrivers = userRepo.GetUnsettledDrivers()
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

        [HttpGet("Dashboard/UnsettledDrivers")]
        public IActionResult GetDashboardUnsettledDrivers()
        {
            try
            {
                var userRepo = new UserRepo(_connectionString);
                var drivers = userRepo.GetUnsettledDrivers();
                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("UnsettledRides/{driverId}")]
        public IActionResult GetUnsettledRidesByDriver(int driverId)
        {
            try
            {
                var rideRepo = new RideRepo(_connectionString);
                var rides = rideRepo.GetUnsettledRidesByDriver(driverId);
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
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

    }
}