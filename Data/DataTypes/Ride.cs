using Square.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Ride
    {
        public int RideId { get; set; }
        public int RouteId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public DateTime CallTime { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? PickupTime { get; set; }
        public DateTime? DropOffTime { get; set; }
        public decimal Cost { get; set; }
        public decimal Tip { get; set; }
        public decimal WaitTimeAmount { get; set; }
        public decimal? DriversCompensation { get; set; }
        public bool Settled { get; set; }
        public int? AssignedToId { get; set; }
        public int? ReassignedToId { get; set; }
        public string? Notes { get; set; }
        public int? DispatchedById { get; set; }
        public string PaymentType { get; set; }
        public string? PaymentTokenId { get; set; } // Square payment token for CC processing
        public bool Reassigned { get; set; }
        public bool Canceled { get; set; }
        public CarType CarType { get; set; }
        public int Passengers { get; set; }
        public bool CarSeat { get; set; }
        public bool IsRecurring { get; set; }
        public string? FlightNumber { get; set; }
        public int? RecurringId { get; set; }

        public Recurring? Recurring { get; set; }
        public Routes? Route { get; set; }

        public Driver? AssignedTo { get; set; }

        public Driver? ReassignedTo { get; set; }

        [JsonIgnore]
        public Dispatcher? DispatchedBy { get; set; }
    }
}
