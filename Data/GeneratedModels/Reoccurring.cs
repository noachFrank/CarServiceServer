using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Reoccurring
{
    public int Id { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly Time { get; set; }

    public DateTime EndDate { get; set; }

    public int RideId { get; set; }

    public virtual Ride Ride { get; set; } = null!;
}
