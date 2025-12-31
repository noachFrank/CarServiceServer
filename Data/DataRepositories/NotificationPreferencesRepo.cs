using DispatchApp.Server.Data.DataTypes;
using DispatchApp.Server.data;
using Microsoft.EntityFrameworkCore;

namespace DispatchApp.Server.Data.DataRepositories
{
    public class NotificationPreferencesRepo
    {
        private readonly string _connectionString;

        public NotificationPreferencesRepo(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<NotificationPreferences?> GetPreferencesAsync(int driverId)
        {
            using var context = new DispatchDbContext(_connectionString);
            return await context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.DriverId == driverId);
        }

        public async Task<NotificationPreferences> CreateDefaultPreferencesAsync(int driverId)
        {
            using var context = new DispatchDbContext(_connectionString);

            var preferences = new NotificationPreferences
            {
                DriverId = driverId,
                MessagesEnabled = true,
                BroadcastMessagesEnabled = true,
                NewCallEnabled = true,
                CallAvailableAgainEnabled = true,
                MyCallReassignedEnabled = true,
                MyCallCanceledEnabled = true,
            };

            context.NotificationPreferences.Add(preferences);
            await context.SaveChangesAsync();

            return preferences;
        }

        public async Task<NotificationPreferences?> UpdatePreferencesAsync(NotificationPreferences preferences)
        {
            using var context = new DispatchDbContext(_connectionString);

            var existing = await context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.DriverId == preferences.DriverId);

            if (existing == null)
            {
                return null;
            }

            existing.MessagesEnabled = preferences.MessagesEnabled;
            existing.BroadcastMessagesEnabled = preferences.BroadcastMessagesEnabled;
            existing.NewCallEnabled = preferences.NewCallEnabled;
            existing.CallAvailableAgainEnabled = preferences.CallAvailableAgainEnabled;
            existing.MyCallReassignedEnabled = preferences.MyCallReassignedEnabled;
            existing.MyCallCanceledEnabled = preferences.MyCallCanceledEnabled;

            await context.SaveChangesAsync();

            return existing;
        }

        public async Task<NotificationPreferences> GetOrCreatePreferencesAsync(int driverId)
        {
            var preferences = await GetPreferencesAsync(driverId);

            if (preferences == null)
            {
                preferences = await CreateDefaultPreferencesAsync(driverId);
            }

            return preferences;
        }

        public async Task<bool> ShouldSendNotificationAsync(int driverId, string notificationType)
        {
            var preferences = await GetOrCreatePreferencesAsync(driverId);

            // Check specific notification type
            return notificationType.ToUpper() switch
            {
                "NEW_MESSAGE" => preferences.MessagesEnabled,
                "BROADCAST_MESSAGE" => preferences.BroadcastMessagesEnabled,
                "NEW_CALL" => preferences.NewCallEnabled,
                "CALL_AVAILABLE_AGAIN" => preferences.CallAvailableAgainEnabled,
                "CALL_UNASSIGNED" => preferences.MyCallReassignedEnabled,
                "CALL_CANCELED" => preferences.MyCallCanceledEnabled,
                _ => true // Unknown types default to enabled
            };
        }
    }
}
