using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Login
{
    public int Id { get; set; }

    public string WorkerType { get; set; } = null!;

    public int WorkerId { get; set; }

    public DateTime LoginTime { get; set; }

    public DateTime? LogoutTime { get; set; }

    public int CallsTaken { get; set; }
}
