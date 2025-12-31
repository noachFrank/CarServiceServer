using System.Net.Http;
using System.Text.Json;

namespace DispatchApp.Server.Services
{
    /// <summary>
    /// Service for calculating travel times between locations using Google Maps Distance Matrix API.
    /// 
    /// HOW IT WORKS:
    /// 1. Takes two addresses (origin and destination)
    /// 2. Calls Google Maps Distance Matrix API
    /// 3. Returns the driving time in minutes
    /// 
    /// This is used to determine if a driver has enough time to get from their current
    /// dropoff location to the next pickup location.
    /// </summary>
    public class GoogleMapsService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private const string DISTANCE_MATRIX_URL = "https://maps.googleapis.com/maps/api/distancematrix/json";

        public GoogleMapsService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }

        public async Task<int> GetTravelTimeMinutesAsync(string originAddress, string destinationAddress)
        {
            try
            {
                // Make the API call
                var response = await GetDistanceAsync(originAddress, destinationAddress);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"GoogleMaps: API call failed with status {response.StatusCode}");
                    return -1;
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<DistanceMatrixResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Check if we got valid results
                if (result?.Status != "OK")
                {
                    Console.WriteLine($"GoogleMaps: API returned status '{result?.Status}'");
                    return -1;
                }

                if (result.Rows == null || result.Rows.Length == 0 ||
                    result.Rows[0].Elements == null || result.Rows[0].Elements.Length == 0)
                {
                    Console.WriteLine("GoogleMaps: No results returned");
                    return -1;
                }

                var element = result.Rows[0].Elements[0];

                if (element.Status != "OK")
                {
                    Console.WriteLine($"GoogleMaps: Element status '{element.Status}' - route not found");
                    return -1;
                }

                // Duration is returned in seconds, convert to minutes
                int durationSeconds = element.Duration?.Value ?? 0;
                int durationMinutes = (int)Math.Ceiling(durationSeconds / 60.0);

                Console.WriteLine($"GoogleMaps: Travel time = {durationMinutes} minutes ({element.Duration?.Text})");

                return durationMinutes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GoogleMaps: Error calculating travel time - {ex.Message}");
                return -1;
            }
        }


        public async Task<HttpResponseMessage> GetDistanceAsync(string originAddress, string destinationAddress)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(originAddress) || string.IsNullOrWhiteSpace(destinationAddress))
                {
                    Console.WriteLine("GoogleMaps: Missing origin or destination address");
                    return null;
                }

                // Build the API URL with query parameters
                var url = $"{DISTANCE_MATRIX_URL}?" +
                    $"origins={Uri.EscapeDataString(originAddress)}" +
                    $"&destinations={Uri.EscapeDataString(destinationAddress)}" +
                    $"&mode=driving" +
                    $"&units=imperial" +
                    $"&key={_apiKey}";

                Console.WriteLine($"GoogleMaps: Calculating travel time from '{originAddress}' to '{destinationAddress}'");

                // Make the API call
                return await _httpClient.GetAsync(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GoogleMaps: Error calculating travel time - {ex.Message}");
                return null;
            }
        }


        public int GetTravelTimeMinutes(string originAddress, string destinationAddress)
        {
            return GetTravelTimeMinutesAsync(originAddress, destinationAddress).GetAwaiter().GetResult();
        }
    }

    #region Google Maps API Response Classes

    /// <summary>
    /// Response from Google Maps Distance Matrix API
    /// </summary>
    public class DistanceMatrixResponse
    {
        public string Status { get; set; }
        public string Error_message { get; set; }
        public string[] DestinationAddresses { get; set; }
        public string[] OriginAddresses { get; set; }
        public DistanceMatrixRow[] Rows { get; set; }
    }

    public class DistanceMatrixRow
    {
        public DistanceMatrixElement[] Elements { get; set; }
    }

    public class DistanceMatrixElement
    {
        public string Status { get; set; }
        public DistanceMatrixValue Distance { get; set; }
        public DistanceMatrixValue Duration { get; set; }
    }

    public class DistanceMatrixValue
    {
        public int Value { get; set; }  // meters or seconds
        public string Text { get; set; } // human readable like "25 mins"
    }

    #endregion
}
