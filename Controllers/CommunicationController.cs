using DispatchApp.Server.Data.DataRepositories;
using DispatchApp.Server.Data.DataTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DispatchApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints
    public class CommunicationController : ControllerBase
    {
        private IWebHostEnvironment _webHostEnvironment;

        private string _connectionString;

        public CommunicationController(IWebHostEnvironment webHostEnvironment, IConfiguration config)
        {
            _webHostEnvironment = webHostEnvironment;
            _connectionString = config.GetConnectionString("ConStr");
        }

        [HttpPost("AddCom")]
        public void AddCom([FromBody] Communication com)
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);
                comRepo.AddCom(com);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpGet("TodaysCom")]
        public IActionResult GetTodaysCom(int driverId)
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);

                var coms = comRepo.GetTodaysCom(driverId);

                if (coms.Any())
                {
                    return Ok(coms);
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

        [HttpGet("AllCom")]
        public IActionResult GetAllCom(int driverId)
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);

                var coms = comRepo.GetAllCom(driverId);

                if (coms.Any())
                {
                    return Ok(coms);
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

        [HttpGet("driverUnreadCount")]
        public IActionResult GetDriverUnreadCount(int driverId)
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);

                var coms = comRepo.GetDriverUnread(driverId);

                if (coms.Any())
                {
                    return Ok(coms.Count);
                }
                else
                {
                    return Ok(0);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("MarkAsRead")]
        public void MarkAsRead([FromBody] List<int> comIds)
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);
                foreach (var comId in comIds)
                    comRepo.MarkAsRead(comId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpGet("BroadcastComs")]
        public IActionResult GetBroadcastComs()
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);

                var coms = comRepo.GetBroadcastComs();

                if (coms.Any())
                {
                    return Ok(coms);
                }
                else
                {
                    return Ok();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("Unread")]
        public IActionResult GetUnread()
        {
            try
            {
                var comRepo = new CommunicationRepo(_connectionString);

                var coms = comRepo.GetUnread();

                if (coms.Any())
                {
                    // Map to include driver name
                    var result = coms.Select(c => new
                    {
                        c.Id,
                        c.Message,
                        c.DriverId,
                        c.From,
                        c.Date,
                        c.Read,
                        DriverName = c.Driver?.Name ?? $"Driver #{c.DriverId}"
                    });
                    return Ok(result);
                }
                else
                {
                    return Ok();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

    }
}
