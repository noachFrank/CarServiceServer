using DispatchApp.Server.Data.DataRepositories;
using DispatchApp.Server.Data.DataTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DispatchApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints
    public class NotificationController : ControllerBase
    {
        private readonly string _connectionString;

        public NotificationController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("ConStr");
        }

        [HttpGet("GetPreferences")]
        public async Task<IActionResult> GetPreferences([FromQuery] int driverId)
        {
            try
            {
                var repo = new NotificationPreferencesRepo(_connectionString);
                var preferences = await repo.GetOrCreatePreferencesAsync(driverId);

                return Ok(new
                {
                    success = true,
                    preferences = new
                    {
                        messagesEnabled = preferences.MessagesEnabled,
                        broadcastMessagesEnabled = preferences.BroadcastMessagesEnabled,
                        newCallEnabled = preferences.NewCallEnabled,
                        callAvailableAgainEnabled = preferences.CallAvailableAgainEnabled,
                        myCallReassignedEnabled = preferences.MyCallReassignedEnabled,
                        myCallCanceledEnabled = preferences.MyCallCanceledEnabled
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("UpdatePreferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            try
            {
                var repo = new NotificationPreferencesRepo(_connectionString);

                var preferences = new NotificationPreferences
                {
                    DriverId = request.DriverId,
                    MessagesEnabled = request.MessagesEnabled,
                    BroadcastMessagesEnabled = request.BroadcastMessagesEnabled,
                    NewCallEnabled = request.NewCallEnabled,
                    CallAvailableAgainEnabled = request.CallAvailableAgainEnabled,
                    MyCallReassignedEnabled = request.MyCallReassignedEnabled,
                    MyCallCanceledEnabled = request.MyCallCanceledEnabled
                };

                var updated = await repo.UpdatePreferencesAsync(preferences);

                if (updated == null)
                {
                    // Create if doesn't exist
                    updated = await repo.CreateDefaultPreferencesAsync(request.DriverId);
                    updated = await repo.UpdatePreferencesAsync(preferences);
                }

                return Ok(new
                {
                    success = true,
                    preferences = new
                    {
                        messagesEnabled = updated.MessagesEnabled,
                        broadcastMessagesEnabled = updated.BroadcastMessagesEnabled,
                        newCallEnabled = updated.NewCallEnabled,
                        callAvailableAgainEnabled = updated.CallAvailableAgainEnabled,
                        myCallReassignedEnabled = updated.MyCallReassignedEnabled,
                        myCallCanceledEnabled = updated.MyCallCanceledEnabled
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class UpdatePreferencesRequest
    {
        public int DriverId { get; set; }
        public bool MessagesEnabled { get; set; }
        public bool BroadcastMessagesEnabled { get; set; }
        public bool NewCallEnabled { get; set; }
        public bool CallAvailableAgainEnabled { get; set; }
        public bool MyCallReassignedEnabled { get; set; }
        public bool MyCallCanceledEnabled { get; set; }
    }
}