using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;

namespace NoufirTours.Models
{
    public class DBContext : DbContext
    {
        public DBContext(DbContextOptions<DBContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Trip> Trips => Set<Trip>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<DeletedTicket> DeletedTickets { get; set; }
        public DbSet<BookingCode> BookingCodes => Set<BookingCode>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();

        public DbSet<Bus> Buses => Set<Bus>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<DriverPhone> DriverPhones => Set<DriverPhone>();

        public DbSet<BusSeat> BusSeats => Set<BusSeat>();

        public DbSet<AutoTripPlan> AutoTripPlans => Set<AutoTripPlan>();
        public DbSet<AutoTripPlanItem> AutoTripPlanItems => Set<AutoTripPlanItem>();

        public DbSet<BookingCollection> BookingCollections => Set<BookingCollection>();

        public DbSet<TechnicalSupport> TechnicalSupports => Set<TechnicalSupport>();

        public DbSet<TripPlace> TripPlaces { get; set; } = default!;

        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // Default Guid generation (SQL Server)
            // =========================
            modelBuilder.Entity<User>()
                .Property(x => x.UserID)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Trip>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Booking>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<AuditLog>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Bus>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Driver>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<DriverPhone>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<BusSeat>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<AutoTripPlan>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<AutoTripPlanItem>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<BookingCollection>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            // =========================
            // Users
            // =========================
            modelBuilder.Entity<User>()
                .HasKey(x => x.UserID);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Username)
                .IsUnique();

            // =========================
            // Trips: DriverUser snapshot relationship
            // =========================
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.DriverUser)
                .WithMany(u => u.DriverTrips)
                .HasForeignKey(t => t.DriverUserId) // Guid?
                .OnDelete(DeleteBehavior.SetNull);

            // =========================
            // Bookings -> Trip
            // =========================
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Trip)
                .WithMany(t => t.Bookings)
                .HasForeignKey(b => b.TripId) // Guid
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.TripId)
                .HasDatabaseName("idx_bookings_trip");

            modelBuilder.Entity<BookingCode>(e =>
            {
                e.HasKey(x => x.BookingId);

                e.HasIndex(x => x.Code).IsUnique();

                // One-to-One: Booking -> BookingCode
                e.HasOne(x => x.Booking)
                 .WithOne(b => b.CodeInfo)
                 .HasForeignKey<BookingCode>(x => x.BookingId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // AuditLogs -> User
            // =========================
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId) // Guid
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("idx_audit_user");

            // =========================
            // Buses unique
            // =========================
            modelBuilder.Entity<Bus>()
                .HasIndex(b => b.BusNumber)
                .IsUnique()
                .HasDatabaseName("ux_buses_bus_number");

            modelBuilder.Entity<Bus>()
                .HasIndex(b => b.ChassisNumber)
                .IsUnique()
                .HasDatabaseName("ux_buses_chassis_number");

            modelBuilder.Entity<Bus>()
                .HasIndex(b => b.PlateNumber)
                .IsUnique()
                .HasDatabaseName("ux_buses_plate_number");

            // =========================
            // Drivers unique
            // =========================
            modelBuilder.Entity<Driver>()
                .HasIndex(d => d.NationalId)
                .IsUnique()
                .HasDatabaseName("ux_drivers_national_id");

            // =========================
            // DriverPhones
            // =========================
            modelBuilder.Entity<DriverPhone>()
                .HasOne(p => p.Driver)
                .WithMany(d => d.Phones)
                .HasForeignKey(p => p.DriverId) // Guid
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DriverPhone>()
                .HasIndex(p => p.PhoneNumber)
                .IsUnique()
                .HasDatabaseName("ux_driver_phones_phone_number");

            modelBuilder.Entity<DriverPhone>()
                .HasIndex(p => new { p.DriverId, p.PhoneNumber })
                .IsUnique()
                .HasDatabaseName("ux_driver_phones_driverid_phone");

            // =========================
            // Trip link bus/driver (Trip.BusId & Trip.DriverId)
            // =========================
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Bus)
                .WithMany(b => b.Trips)
                .HasForeignKey(t => t.BusId) // Guid?
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Driver)
                .WithMany(d => d.Trips)
                .HasForeignKey(t => t.DriverId) // Guid?
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Trip>()
                .HasIndex(t => t.BusId)
                .HasDatabaseName("idx_trips_bus_id");

            modelBuilder.Entity<Trip>()
                .HasIndex(t => t.DriverId)
                .HasDatabaseName("idx_trips_driver_id");

            modelBuilder.Entity<Trip>()
                .HasIndex(t => new { t.DepartDate, t.DepartTime })
                .HasDatabaseName("idx_trips_depart_dt");

            modelBuilder.Entity<Trip>()
                .HasIndex(t => t.IsArchivedInt)
                .HasDatabaseName("idx_trips_archived");

            modelBuilder.Entity<Trip>()
                .HasIndex(t => t.IsActiveInt)
                .HasDatabaseName("idx_trips_active");

            // =========================
            // Trips decimals precision
            // =========================
            modelBuilder.Entity<Trip>()
                .Property(t => t.SeatPriceGo)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Trip>()
                .Property(t => t.SeatPriceReturn)
                .HasPrecision(18, 2);

            // =========================
            // Trips unique constraints (filters use DB column names)
            // =========================
            modelBuilder.Entity<Trip>()
                .HasIndex(t => new { t.DepartDate, t.TripName })
                .IsUnique()
                .HasDatabaseName("ux_trips_depdate_tripname")
                .HasFilter("[is_archived] = 0");

            modelBuilder.Entity<Trip>()
                .HasIndex(t => new { t.DepartDate, t.BusId })
                .IsUnique()
                .HasDatabaseName("ux_trips_depdate_busid")
                .HasFilter("[is_archived] = 0 AND [bus_id] IS NOT NULL");

            modelBuilder.Entity<Trip>()
                .HasIndex(t => new { t.DepartDate, t.DriverId })
                .IsUnique()
                .HasDatabaseName("ux_trips_depdate_driverid")
                .HasFilter("[is_archived] = 0 AND [driver_id] IS NOT NULL");

            // =========================
            // BusSeats
            // =========================
            modelBuilder.Entity<BusSeat>()
                .HasOne(s => s.Bus)
                .WithMany(b => b.Seats)
                .HasForeignKey(s => s.BusId) // Guid
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BusSeat>()
                .HasIndex(s => new { s.BusId, s.SeatCode })
                .IsUnique()
                .HasDatabaseName("ux_bus_seats_busid_seatcode");

            // =========================
            // AutoTripPlan / Items
            // =========================
            modelBuilder.Entity<AutoTripPlan>(e =>
            {
                e.ToTable("AutoTripPlans");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.SpecificDate).HasMaxLength(20);
            });

            modelBuilder.Entity<AutoTripPlanItem>(e =>
            {
                e.ToTable("AutoTripPlanItems");
                e.HasKey(x => x.Id);

                e.Property(x => x.DepartTime).HasMaxLength(5).IsRequired();
                e.Property(x => x.TripName).HasMaxLength(120).IsRequired();

                e.Property(x => x.PickupLat).HasPrecision(10, 7);
                e.Property(x => x.PickupLon).HasPrecision(10, 7);

                e.Property(x => x.SeatPriceGo).HasPrecision(18, 2);
                e.Property(x => x.SeatPriceReturn).HasPrecision(18, 2);

                e.HasOne(x => x.Plan)
                    .WithMany(p => p.Items)
                    .HasForeignKey(x => x.PlanId) // Guid
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Bus)
                    .WithMany()
                    .HasForeignKey(x => x.BusId) // Guid?
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Driver)
                    .WithMany()
                    .HasForeignKey(x => x.DriverId) // Guid?
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => new { x.PlanId, x.OrderNo }).IsUnique();
            });

            // =========================
            // Bookings -> CreatedByUser / CanceledByUser
            // =========================
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.CreatedByUser)
                .WithMany(u => u.BookingTrips)
                .HasForeignKey(b => b.CreatedByUserId) // Guid
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.CreatedByUserId)
                .HasDatabaseName("idx_bookings_created_by_user");

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.CanceledByUser)
                .WithMany(u => u.CanceledBookings)
                .HasForeignKey(b => b.CanceledByUserId) // Guid?
                .OnDelete(DeleteBehavior.SetNull);

            // =========================
            // BookingCollections
            // =========================
            modelBuilder.Entity<BookingCollection>()
                .HasOne(c => c.Booking)
                .WithMany(b => b.Collections)
                .HasForeignKey(c => c.BookingId) // Guid
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingCollection>()
                .HasOne(c => c.CollectedByUser)
                .WithMany(u => u.CollectedPayments)
                .HasForeignKey(c => c.CollectedByUserId) // Guid
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookingCollection>()
                .HasIndex(c => c.BookingId)
                .HasDatabaseName("idx_booking_collections_booking");

            modelBuilder.Entity<BookingCollection>()
                .HasIndex(c => c.CollectedByUserId)
                .HasDatabaseName("idx_booking_collections_user");

            // =========================
            // Booking -> ReturnTrip (optional)
            // =========================
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ReturnTrip)
                .WithMany()
                .HasForeignKey(b => b.ReturnTripId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.ReturnTripId)
                .HasDatabaseName("idx_bookings_return_trip");

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.DestinationPlace)
                .WithMany()
                .HasForeignKey(b => b.DestinationPlaceId)
                .OnDelete(DeleteBehavior.NoAction); // <-- كان SetNull

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ReturnDestinationPlace)
                .WithMany()
                .HasForeignKey(b => b.ReturnDestinationPlaceId)
                .OnDelete(DeleteBehavior.NoAction); // <-- كان SetNull

            // =========================
            // Customers
            // =========================
            modelBuilder.Entity<Customer>()
                .ToTable("customers");

            modelBuilder.Entity<Customer>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Customer>()
                .Property(c => c.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Customer>()
                .Property(c => c.FullName)
                .HasMaxLength(120)
                .IsRequired();

            modelBuilder.Entity<Customer>()
                .Property(c => c.Phone)
                .HasMaxLength(30)
                .IsRequired();

            modelBuilder.Entity<Customer>()
                .HasIndex(c => new { c.FullName, c.Phone })
                .IsUnique()
                .HasDatabaseName("ux_customers_name_phone");
        }
    }
}