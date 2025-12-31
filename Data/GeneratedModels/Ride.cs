using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Ride
{
    public int RideId { get; set; }

    public int RouteId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string CustomerPhoneNumber { get; set; } = null!;

    public DateTime CallTime { get; set; }

    public DateTime? PickupTime { get; set; }

    public DateTime? DropOffTime { get; set; }

    public decimal Cost { get; set; }

    public decimal? DriversCompensation { get; set; }

    public int? AssignedToId { get; set; }

    public int? ReassignedToId { get; set; }

    public string? Notes { get; set; }

    public int? DispatchedById { get; set; }

    public string PaymentType { get; set; } = null!;

    public bool Reassigned { get; set; }

    public bool Canceled { get; set; }

    public DateTime ScheduledFor { get; set; }

    public int CarType { get; set; }

    public int Passengers { get; set; }

    public decimal Tip { get; set; }

    public decimal WaitTimeAmount { get; set; }

    public bool CarSeat { get; set; }

    public bool IsReoccurring { get; set; }

    public virtual Driver? AssignedTo { get; set; }

    public virtual Dispatcher? DispatchedBy { get; set; }

    public virtual Driver? ReassignedTo { get; set; }

    public virtual Reoccurring? Reoccurring { get; set; }

    public virtual Route Route { get; set; } = null!;
}
