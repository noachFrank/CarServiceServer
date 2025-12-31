namespace DispatchApp.Server.Data.DataTypes
{
    public class NotificationPreferences
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        
        // Individual notification types
        public bool MessagesEnabled { get; set; } = true;
        public bool BroadcastMessagesEnabled { get; set; } = true;
        public bool NewCallEnabled { get; set; } = true;
        public bool CallAvailableAgainEnabled { get; set; } = true;
        public bool MyCallReassignedEnabled { get; set; } = true;
        public bool MyCallCanceledEnabled { get; set; } = true;
        
        // Navigation property
        public virtual Driver? Driver { get; set; }
    }
}
