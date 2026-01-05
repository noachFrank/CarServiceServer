using DispatchApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DispatchApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require JWT authentication
    public class PaymentController : ControllerBase
    {
        private readonly SquarePaymentService _paymentService;

        public PaymentController(SquarePaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Tokenizes and charges a credit card from manually entered details
        /// POST /api/Payment/TokenizeAndChargeCard
        /// Used for Driver CC payments where card is entered in the driver app
        /// </summary>
        [HttpPost("TokenizeAndChargeCard")]
        public async Task<IActionResult> TokenizeAndChargeCard([FromBody] TokenizeAndChargeRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.CardNumber))
                {
                    return BadRequest(new { success = false, message = "Card number is required", errorCode = "INVALID_REQUEST" });
                }

                if (string.IsNullOrEmpty(request.ExpiryMonth) || string.IsNullOrEmpty(request.ExpiryYear))
                {
                    return BadRequest(new { success = false, message = "Expiry date is required", errorCode = "INVALID_REQUEST" });
                }

                if (string.IsNullOrEmpty(request.Cvv))
                {
                    return BadRequest(new { success = false, message = "CVV is required", errorCode = "INVALID_REQUEST" });
                }

                if (string.IsNullOrEmpty(request.CardholderName))
                {
                    return BadRequest(new { success = false, message = "Cardholder name is required", errorCode = "INVALID_REQUEST" });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new { success = false, message = "Amount must be greater than 0", errorCode = "INVALID_REQUEST" });
                }

                // Process the payment
                var result = await _paymentService.TokenizeAndChargeCard(
                    request.CardNumber,
                    request.ExpiryMonth,
                    request.ExpiryYear,
                    request.Cvv,
                    request.CardholderName,
                    request.Amount,
                    request.RideId,
                    request.Note
                );

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        paymentId = result.PaymentId
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        errorCode = result.ErrorCode
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred processing the payment",
                    errorCode = "SERVER_ERROR"
                });
            }
        }

        /// <summary>
        /// Charges a credit card using a tokenized payment source
        /// POST /api/Payment/ChargeCard
        /// </summary>
        [HttpPost("ChargeCard")]
        public async Task<IActionResult> ChargeCard([FromBody] ChargeCardRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.PaymentTokenId))
                {
                    return BadRequest(new { success = false, message = "Payment token is required" });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new { success = false, message = "Amount must be greater than 0" });
                }

                // Process the payment
                var result = await _paymentService.ChargeCard(
                    request.PaymentTokenId,
                    request.Amount,
                    request.RideId,
                    request.Note
                );

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        paymentId = result.PaymentId
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        errorCode = result.ErrorCode
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred processing the payment"
                });
            }
        }

        /// <summary>
        /// Verifies that a payment token is valid
        /// POST /api/Payment/VerifyToken
        /// </summary>
        [HttpPost("VerifyToken")]
        public async Task<IActionResult> VerifyToken([FromBody] VerifyTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.PaymentTokenId))
                {
                    return BadRequest(new { success = false, message = "Payment token is required" });
                }

                var result = await _paymentService.VerifyPaymentToken(request.PaymentTokenId);

                return Ok(new
                {
                    success = result.IsValid,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred verifying the token"
                });
            }
        }
    }

    // Request DTOs
    public class ChargeCardRequest
    {
        public string PaymentTokenId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int RideId { get; set; }
        public string? Note { get; set; }
    }

    public class TokenizeAndChargeRequest
    {
        public string CardNumber { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty;
        public string ExpiryYear { get; set; } = string.Empty;
        public string Cvv { get; set; } = string.Empty;
        public string CardholderName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int RideId { get; set; }
        public string? Note { get; set; }
    }

    public class VerifyTokenRequest
    {
        public string PaymentTokenId { get; set; } = string.Empty;
    }
}
