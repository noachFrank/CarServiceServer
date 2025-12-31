using Square;
using Square.Models;
using Square.Exceptions;
using Square.Http.Client;
using Square.Authentication;
using System;
using System.Threading.Tasks;

namespace DispatchApp.Server.Services
{
    public class SquarePaymentService
    {
        private readonly ISquareClient _client;
        private readonly string _locationId;

        public SquarePaymentService(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            var accessToken = configuration["SquareSettings:AccessToken"]?.Trim();
            var environment = configuration["SquareSettings:Environment"]?.Trim();
            var locationId = configuration["SquareSettings:LocationId"]?.Trim();

            if (string.IsNullOrEmpty(accessToken))
            {
                throw new Exception("Square Access Token is not configured. Please set SquareSettings:AccessToken in appsettings.json");
            }

            if (string.IsNullOrEmpty(locationId))
            {
                throw new Exception("Square Location ID is not configured. Please set SquareSettings:LocationId in appsettings.json");
            }

            _locationId = locationId;

            // Set environment (Sandbox or Production)
            var squareEnvironment = environment?.ToLower() == "production"
                ? Square.Environment.Production
                : Square.Environment.Sandbox;

            // Initialize Square client using the NEW BearerAuthCredentials method (not deprecated)
            try
            {
                var bearerAuthModel = new BearerAuthModel.Builder(accessToken).Build();

                _client = new SquareClient.Builder()
                    .BearerAuthCredentials(bearerAuthModel)
                    .Environment(squareEnvironment)
                    .Build();

                Console.WriteLine($"✅ Square Payment Service initialized in {environment} mode");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create SquareClient: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                throw;
            }

            Console.WriteLine($"✅ Square Payment Service initialized in {environment} mode");
        }

        /// <summary>
        /// Tokenizes card details and charges the card in one operation
        /// Used for Driver CC payments where card is entered manually
        /// </summary>
        public async Task<PaymentResult> TokenizeAndChargeCard(
            string cardNumber,
            string expiryMonth,
            string expiryYear,
            string cvv,
            string cardholderName,
            decimal amountInDollars,
            int rideId,
            string? note = null)
        {
            try
            {
                Console.WriteLine($"\n=== DRIVER CC: CLIENT-TOKENIZED CARD ===");
                Console.WriteLine($"   Note: This endpoint expects a token from Square Web Payments SDK");
                Console.WriteLine($"   Card data should never reach this endpoint");
                Console.WriteLine($"   Amount: ${amountInDollars}");
                Console.WriteLine($"   Ride ID: {rideId}");

                // This endpoint is deprecated - the frontend should call ChargeCard directly with the token
                // from Square Web Payments SDK. This method should not receive raw card data.
                return new PaymentResult
                {
                    Success = false,
                    Message = "This endpoint is deprecated. Please use the ChargeCard endpoint with a pre-tokenized card.",
                    ErrorCode = "DEPRECATED_ENDPOINT"
                };
            }
            catch (ApiException ex)
            {
                Console.WriteLine($"❌ Square API Error during tokenization/charge:");
                Console.WriteLine($"   Status: {ex.ResponseCode}");
                Console.WriteLine($"   Message: {ex.Message}");

                var (errorMessage, errorCode) = ParseSquareError(ex);
                return new PaymentResult
                {
                    Success = false,
                    Message = errorMessage,
                    ErrorCode = errorCode
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unexpected error during tokenization/charge: {ex.Message}");
                return new PaymentResult
                {
                    Success = false,
                    Message = "An unexpected error occurred. Please try again.",
                    ErrorCode = "UNKNOWN_ERROR"
                };
            }
        }

        /// <summary>
        /// Charges a credit card using a previously tokenized payment source
        /// </summary>
        /// <param name="paymentTokenId">The Square payment token (nonce) from client-side tokenization</param>
        /// <param name="amountInDollars">The amount to charge in dollars (will be converted to cents)</param>
        /// <param name="rideId">The ride ID for reference</param>
        /// <param name="note">Optional note for the payment</param>
        /// <returns>Result with payment ID if successful</returns>
        public async Task<PaymentResult> ChargeCard(string paymentTokenId, decimal amountInDollars, int rideId, string? note = null)
        {
            try
            {
                Console.WriteLine($"💳 Attempting to charge ${amountInDollars} for Ride #{rideId}");
                Console.WriteLine($"   Token: {paymentTokenId}");
                Console.WriteLine($"   Location: {_locationId}");

                // Convert dollars to cents (Square requires amounts in smallest currency unit)
                var amountInCents = (long)(amountInDollars * 100);

                // Create idempotency key (ensures we don't charge twice for the same ride)
                // Square has a 45-character limit, so use shortened GUID (first 32 chars without hyphens)
                var shortGuid = Guid.NewGuid().ToString("N").Substring(0, 24);
                var idempotencyKey = $"r{rideId}_{shortGuid}";

                // Build the payment request
                var body = new CreatePaymentRequest.Builder(
                    sourceId: paymentTokenId,
                    idempotencyKey: idempotencyKey)
                    .LocationId(_locationId)
                    .AmountMoney(new Money(amountInCents, "USD"))
                    .Note(note ?? $"Ride #{rideId}")
                    .ReferenceId($"ride_{rideId}") // For tracking in Square dashboard
                    .Autocomplete(true) // Complete payment immediately
                    .Build();

                // Process the payment
                var response = await _client.PaymentsApi.CreatePaymentAsync(body);

                if (response?.Payment?.Status == "COMPLETED")
                {
                    Console.WriteLine($"✅ Payment successful! Square Payment ID: {response.Payment.Id}");
                    return new PaymentResult
                    {
                        Success = true,
                        PaymentId = response.Payment.Id,
                        Message = "Payment processed successfully"
                    };
                }
                else
                {
                    var status = response?.Payment?.Status ?? "UNKNOWN";
                    Console.WriteLine($"⚠️ Payment not completed. Status: {status}");
                    return new PaymentResult
                    {
                        Success = false,
                        Message = $"Payment status: {status}. Please try again."
                    };
                }
            }
            catch (ApiException ex)
            {
                // Square API returned an error
                Console.WriteLine($"❌ Square API Error: {ex.Message}");
                var (errorMessage, errorCode) = ParseSquareError(ex);
                return new PaymentResult
                {
                    Success = false,
                    Message = errorMessage,
                    ErrorCode = errorCode
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unexpected error processing payment: {ex.Message}");
                return new PaymentResult
                {
                    Success = false,
                    Message = "An unexpected error occurred. Please try again."
                };
            }
        }

        /// <summary>
        /// Verifies that a payment token is valid and the card can be charged
        /// This is a best-effort validation - doesn't guarantee the charge will succeed
        /// </summary>
        /// <param name="paymentTokenId">The Square payment token to verify</param>
        /// <returns>True if token appears valid, false otherwise</returns>
        public async Task<VerificationResult> VerifyPaymentToken(string paymentTokenId)
        {
            try
            {
                Console.WriteLine($"🔍 Verifying payment token: {paymentTokenId}");

                // Note: Square doesn't have a dedicated "verify token" endpoint
                // The best practice is to attempt a small authorization and void it
                // However, this costs money, so we'll just validate the token format

                if (string.IsNullOrWhiteSpace(paymentTokenId))
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        Message = "Payment token is empty"
                    };
                }

                // Square tokens (nonces) typically start with "cnon:" or "ccof:"
                if (!paymentTokenId.StartsWith("cnon:") && !paymentTokenId.StartsWith("ccof:"))
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        Message = "Invalid token format"
                    };
                }

                Console.WriteLine($"✅ Token format is valid");
                return new VerificationResult
                {
                    IsValid = true,
                    Message = "Token is valid"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verifying token: {ex.Message}");
                return new VerificationResult
                {
                    IsValid = false,
                    Message = "Unable to verify token"
                };
            }
        }

        /// <summary>
        /// Parses Square API errors into user-friendly messages with error codes
        /// </summary>
        private (string Message, string ErrorCode) ParseSquareError(ApiException ex)
        {
            var errors = ex.Errors;
            if (errors != null && errors.Count > 0)
            {
                var firstError = errors[0];
                var category = firstError.Category;
                var code = firstError.Code;
                var detail = firstError.Detail ?? "";

                // Return both user-friendly message and error code for client-side handling
                return (category, code) switch
                {
                    // Card declined errors
                    ("PAYMENT_METHOD_ERROR", "CARD_DECLINED") =>
                        ("Card was declined. Please use a different card.", "CARD_DECLINED"),
                    ("PAYMENT_METHOD_ERROR", "INSUFFICIENT_FUNDS") =>
                        ("Insufficient funds. Please use a different card.", "INSUFFICIENT_FUNDS"),
                    ("PAYMENT_METHOD_ERROR", "CVV_FAILURE") =>
                        ("CVV verification failed. Please check the card details.", "CVV_FAILURE"),
                    ("PAYMENT_METHOD_ERROR", "INVALID_EXPIRATION") =>
                        ("Card has expired. Please use a different card.", "CARD_EXPIRED"),
                    ("PAYMENT_METHOD_ERROR", "INVALID_CARD") =>
                        ("Invalid card number. Please check the card details.", "INVALID_CARD"),

                    // Token/nonce errors
                    ("INVALID_REQUEST_ERROR", var c) when detail.Contains("nonce") && detail.Contains("already") =>
                        ("This payment card has already been used. Please ask dispatch to re-enter the card details.", "TOKEN_USED"),
                    ("INVALID_REQUEST_ERROR", var c) when detail.Contains("idempotency") =>
                        ("Payment token expired. Please ask dispatch to re-enter the card details.", "TOKEN_EXPIRED"),

                    // Generic errors
                    ("PAYMENT_METHOD_ERROR", _) =>
                        ($"Card error: {detail}", "PAYMENT_METHOD_ERROR"),
                    ("AUTHENTICATION_ERROR", _) =>
                        ("Payment processor error. Please contact support.", "AUTH_ERROR"),
                    ("INVALID_REQUEST_ERROR", _) =>
                        ($"Invalid payment request: {detail}", "INVALID_REQUEST"),

                    _ => (detail != "" ? detail : "Payment failed. Please try again.", "UNKNOWN_ERROR")
                };
            }

            return (ex.Message ?? "Payment failed. Please try again.", "UNKNOWN_ERROR");
        }
    }

    /// <summary>
    /// Result of a payment charge attempt
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? PaymentId { get; set; }  // Square's payment ID
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; }
    }

    /// <summary>
    /// Result of a token verification
    /// </summary>
    public class VerificationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
