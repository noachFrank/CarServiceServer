using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Car
{
    public int CarId { get; set; }

    public int DriverId { get; set; }

    public int Type { get; set; }

    public string Make { get; set; } = null!;

    public string Model { get; set; } = null!;

    public int Year { get; set; }

    public string Color { get; set; } = null!;

    public string LicensePlate { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public int Seats { get; set; }

    public virtual Driver Driver { get; set; } = null!;
}
