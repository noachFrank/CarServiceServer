using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Driver
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string License { get; set; } = null!;

    public DateTime? JoinedDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool Active { get; set; }

    public bool OnJob { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? ExpoPushToken { get; set; }

    public virtual ICollection<Car> Cars { get; set; } = new List<Car>();

    public virtual ICollection<Communication> Communications { get; set; } = new List<Communication>();

    public virtual ICollection<Ride> RideAssignedTos { get; set; } = new List<Ride>();

    public virtual ICollection<Ride> RideReassignedTos { get; set; } = new List<Ride>();
}
