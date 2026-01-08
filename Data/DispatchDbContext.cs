using DispatchApp.Server.Data.DataTypes;
using Microsoft.EntityFrameworkCore;

namespace DispatchApp.Server.data;

public class DispatchDbContext : DbContext
{
    private string _connectionString;

    public DispatchDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }

    public DispatchDbContext(DbContextOptions<DispatchDbContext> options)
    : base(options)
    {
    }


    public DbSet<Ride> Rides { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Car> Cars { get; set; }
    public DbSet<Routes> Routes { get; set; }
    public DbSet<Dispatcher> Dispatchers { get; set; }
    public DbSet<Login> Logins { get; set; }
    public DbSet<Communication> Communications { get; set; }
    public DbSet<Recurring> Recurrings { get; set; }
    public DbSet<NotificationPreferences> NotificationPreferences { get; set; }
    public DbSet<Invoice> Invoices { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ride
        modelBuilder.Entity<Ride>()
            .HasKey(r => r.RideId);

        // Configure Ride -> Route relationship (1:1)
        modelBuilder.Entity<Ride>()
            .HasOne(r => r.Route)
            .WithMany(rt => rt.Rides)
            .HasForeignKey(r => r.RouteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ride>()
            .HasOne(r => r.AssignedTo)
            .WithMany(d => d.AssignedRides)
            .HasForeignKey(r => r.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ride>()
            .HasOne(r => r.ReassignedTo)
            .WithMany(d => d.ReassignedRides)
            .HasForeignKey(r => r.ReassignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ride>()
            .HasOne(r => r.DispatchedBy)
            .WithMany()
            .HasForeignKey(r => r.DispatchedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Routes
        modelBuilder.Entity<Routes>()
            .HasKey(rt => rt.RouteId);

        // Reoccurring
        modelBuilder.Entity<Recurring>()
            .HasKey(rec => rec.Id);

        //modelBuilder.Entity<Recurring>()
        //    .HasMany(rec => rec.ri)
        //    .WithOne(r => r.Reoccurring)
        //    .HasForeignKey(rec => rec.RideId)
        //    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Ride>()
        .HasOne(r => r.Recurring)
        .WithMany(d => d.Rides)
        .HasForeignKey(r => r.RecurringId)
        .OnDelete(DeleteBehavior.Restrict);

        // Driver
        modelBuilder.Entity<Driver>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<Driver>()
            .HasMany(d => d.Cars)
            .WithOne(c => c.Driver)
            .HasForeignKey(c => c.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        // Car
        modelBuilder.Entity<Car>()
            .HasKey(c => c.CarId);

        // Communication
        modelBuilder.Entity<Communication>()
            .HasOne(c => c.Driver)
            .WithMany(d => d.Communications)
            .HasForeignKey(c => c.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        // Property configurations
        modelBuilder.Entity<Ride>()
            .Property(r => r.CustomerPhoneNumber)
            .HasMaxLength(32);

        // Configure decimal properties for Ride with precision and scale
        modelBuilder.Entity<Ride>()
            .Property(r => r.Cost)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Ride>()
            .Property(r => r.DriversCompensation)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Ride>()
            .Property(r => r.Tip)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Ride>()
            .Property(r => r.WaitTimeAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Driver>()
            .Property(d => d.Name)
            .HasMaxLength(128);

        modelBuilder.Entity<Driver>()
            .Property(d => d.License)
            .HasMaxLength(64);

        modelBuilder.Entity<Driver>()
            .Property(d => d.PhoneNumber)
            .HasMaxLength(32);

        modelBuilder.Entity<Dispatcher>()
            .Property(d => d.PhoneNumber)
            .HasMaxLength(32);

        modelBuilder.Entity<Car>()
            .Property(c => c.Make)
            .HasMaxLength(64);

        modelBuilder.Entity<Car>()
            .Property(c => c.Model)
            .HasMaxLength(64);

        modelBuilder.Entity<Car>()
            .Property(c => c.Color)
            .HasMaxLength(32);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Pickup)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.DropOff)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop1)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop2)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop3)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop4)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop5)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop6)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop7)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop8)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop9)
            .HasMaxLength(128);

        modelBuilder.Entity<Routes>()
            .Property(rt => rt.Stop10)
            .HasMaxLength(128);

        // NotificationPreferences
        modelBuilder.Entity<NotificationPreferences>()
            .HasKey(np => np.Id);

        modelBuilder.Entity<NotificationPreferences>()
            .HasOne(np => np.Driver)
            .WithOne()
            .HasForeignKey<NotificationPreferences>(np => np.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationPreferences>()
            .HasIndex(np => np.DriverId)
            .IsUnique();

        // Invoice
        modelBuilder.Entity<Invoice>()
            .HasKey(i => i.Id);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Driver)
            .WithMany()
            .HasForeignKey(i => i.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.InvoiceNumber)
            .HasMaxLength(64);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.DriverName)
            .HasMaxLength(128);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.DriverUsername)
            .HasMaxLength(64);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.FilePath)
            .HasMaxLength(512);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.TotalOwedToDriver)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Invoice>()
            .Property(i => i.TotalOwedByDriver)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Invoice>()
            .Property(i => i.NetAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.InvoiceNumber)
            .IsUnique();

    }
}
