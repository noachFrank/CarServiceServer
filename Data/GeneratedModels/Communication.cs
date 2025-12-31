using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Communication
{
    public int Id { get; set; }

    public string Message { get; set; } = null!;

    public int DriverId { get; set; }

    public string From { get; set; } = null!;

    public DateTime Date { get; set; }

    public bool Read { get; set; }

    public virtual Driver Driver { get; set; } = null!;
}
