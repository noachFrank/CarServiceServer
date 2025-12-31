using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DispatchApp.Server.Data.DataRepositories;

namespace DispatchApp.Server.Services
{
    /// <summary>
    /// Service for sending push notifications to drivers via Expo's Push Notification API.
    /// 
    /// HOW IT WORKS:
    /// 1. Expo provides a free push notification service that works with React Native apps
    /// 2. When a driver's app starts, it gets an "Expo Push Token" (unique to that device)
    /// 3. We store that token in our database (Driver.ExpoPushToken)
    /// 4. When we want to notify a driver, we send a request to Expo's API with their token
    /// 5. Expo handles the complexity of delivering to Apple (APNs) or Google (FCM)
    /// 
    /// API ENDPOINT: https://exp.host/--/api/v2/push/send
    /// 
    /// IMPORTANT: Expo's push service is FREE for unlimited notifications!
    /// No API key needed for basic usage (the push token itself authenticates the request).
    /// </summary>
    public class PushNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _connectionString;
        private const string ExpoPushUrl = "https://exp.host/--/api/v2/push/send";

        public PushNotificationService(string? connectionString = null)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            _connectionString = connectionString;
        }

        /// <summary>
        /// Sends a push notification to a single device.
        /// Checks notification preferences before sending if connection string is provided.
        /// </summary>
        /// <param name="expoPushToken">The driver's Expo Push Token (e.g., "ExponentPushToken[xxxxxx]")</param>
        /// <param name="title">The notification title (shown in bold)</param>
        /// <param name="body">The notification body text</param>
        /// <param name="data">Optional data payload - passed to the app when notification is tapped</param>
        /// <param name="driverId">Optional driver ID to check notification preferences</param>
        /// <param name="notificationType">Type of notification (e.g., NEW_CALL, NEW_MESSAGE)</param>
        /// <returns>True if sent successfully, false otherwise</returns>
        public async Task<bool> SendPushNotificationAsync(
            string expoPushToken,
            string title,
            string body,
            object? data = null,
            int? driverId = null,
            string? notificationType = null)
        {
            if (string.IsNullOrEmpty(expoPushToken))
            {
                Console.WriteLine("Cannot send push notification: No push token provided");
                return false;
            }

            // Check notification preferences if driver ID and connection string provided
            if (driverId.HasValue && !string.IsNullOrEmpty(_connectionString) && !string.IsNullOrEmpty(notificationType))
            {
                var preferencesRepo = new NotificationPreferencesRepo(_connectionString);
                var shouldSend = await preferencesRepo.ShouldSendNotificationAsync(driverId.Value, notificationType);

                if (!shouldSend)
                {
                    Console.WriteLine($"🔕 Notification blocked by user preferences: Driver {driverId.Value}, Type: {notificationType}");
                    return false;
                }
            }

            // Validate the token format (should start with ExponentPushToken)
            if (!expoPushToken.StartsWith("ExponentPushToken"))
            {
                Console.WriteLine($"Invalid push token format: {expoPushToken}");
                return false;
            }

            try
            {
                var notification = new
                {
                    to = expoPushToken,
                    title = title,
                    body = body,
                    // "data" is passed to the app when the notification is tapped
                    // This is how we know which screen to open, which call to show, etc.
                    data = data,
                    // Sound settings
                    sound = "default",
                    // Priority settings for reliable delivery
                    priority = "high",
                    // Channel ID for Android notification grouping
                    channelId = "default"
                };

                var json = JsonSerializer.Serialize(notification);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ExpoPushUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Push notification sent successfully to token: {expoPushToken.Substring(0, 30)}...");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ Failed to send push notification: {response.StatusCode} - {responseBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending push notification: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends push notifications to multiple devices at once (batch send).
        /// More efficient than sending one at a time.
        /// </summary>
        /// <param name="notifications">List of notifications to send</param>
        public async Task SendBatchPushNotificationsAsync(List<PushNotificationRequest> notifications)
        {
            if (notifications == null || !notifications.Any())
            {
                Console.WriteLine("No notifications to send");
                return;
            }

            // Filter out invalid tokens
            var validNotifications = notifications
                .Where(n => !string.IsNullOrEmpty(n.ExpoPushToken) && n.ExpoPushToken.StartsWith("ExponentPushToken"))
                .ToList();

            if (!validNotifications.Any())
            {
                Console.WriteLine("No valid push tokens to send notifications to");
                return;
            }

            try
            {
                // Expo accepts an array of notifications for batch sending
                var payload = validNotifications.Select(n => new
                {
                    to = n.ExpoPushToken,
                    title = n.Title,
                    body = n.Body,
                    data = n.Data,
                    sound = "default",
                    priority = "high",
                    channelId = "default"
                }).ToList();

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ExpoPushUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Batch push notifications sent successfully to {validNotifications.Count} devices");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to send batch push notifications: {response.StatusCode} - {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending batch push notifications: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Request object for batch push notifications
    /// </summary>
    public class PushNotificationRequest
    {
        public string ExpoPushToken { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
