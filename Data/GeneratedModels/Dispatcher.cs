using System;
using System.Collections.Generic;

namespace DispatchApp.Server.Data.GeneratedModels;

public partial class Dispatcher
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string Password { get; set; } = null!;

    public DateTime DateJoined { get; set; }

    public bool IsActive { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public bool IsAdmin { get; set; }

    public string Email { get; set; } = null!;

    public virtual ICollection<Ride> Rides { get; set; } = new List<Ride>();
}
