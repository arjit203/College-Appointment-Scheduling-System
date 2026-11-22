using CampusConnect.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusConnect.Data;

// The DbContext is the "bridge" between our C# code and the SQL Server database.
// - Each DbSet below represents one table.
// - We query tables with LINQ through it, e.g. _context.Departments.ToListAsync().
// - It remembers changes to objects and saves them with SaveChangesAsync().
//
// We inherit from IdentityDbContext<ApplicationUser> (not plain DbContext)
// so that ASP.NET Core Identity's tables (AspNetUsers, AspNetRoles,
// AspNetUserRoles, ...) are created in the SAME database as our tables.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // The options (which database, which connection string) are passed in
    // from Program.cs through Dependency Injection.
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ----- Tables -----
    // ApplicationUser has no DbSet here: IdentityDbContext already provides
    // it as "Users" (the AspNetUsers table).
    public DbSet<Department> Departments { get; set; }
    public DbSet<FacultyProfile> FacultyProfiles { get; set; }
    public DbSet<AppointmentType> AppointmentTypes { get; set; }
    public DbSet<AvailabilitySlot> AvailabilitySlots { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // OnModelCreating lets us describe relationships and rules that EF Core
    // cannot guess on its own. This style is called the "Fluent API".
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MUST be called first: it configures all the Identity tables.
        base.OnModelCreating(modelBuilder);

        // ----- Department 1 -> many FacultyProfiles -----
        // Restrict = a department cannot be deleted while faculty belong to it.
        modelBuilder.Entity<FacultyProfile>()
            .HasOne(faculty => faculty.Department)
            .WithMany(department => department.FacultyProfiles)
            .HasForeignKey(faculty => faculty.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ----- ApplicationUser 1 -> 1 FacultyProfile -----
        // EF Core adds a UNIQUE index on UserId, so one user = one profile.
        // Cascade = if the user is deleted, their faculty profile goes too.
        modelBuilder.Entity<FacultyProfile>()
            .HasOne(faculty => faculty.User)
            .WithOne(user => user.FacultyProfile)
            .HasForeignKey<FacultyProfile>(faculty => faculty.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ----- FacultyProfile 1 -> many AvailabilitySlots -----
        // Cascade = removing a faculty profile removes their free slots.
        modelBuilder.Entity<AvailabilitySlot>()
            .HasOne(slot => slot.FacultyProfile)
            .WithMany(faculty => faculty.AvailabilitySlots)
            .HasForeignKey(slot => slot.FacultyProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // ----- Appointment relationships -----
        // All four use Restrict. Two reasons:
        // 1. Appointment history should never disappear silently.
        // 2. SQL Server does not allow "multiple cascade paths". Deleting a user
        //    could reach Appointments directly (as student) AND through
        //    FacultyProfile -> Appointments. SQL Server refuses that, so we
        //    cascade nothing into Appointments.

        // ApplicationUser (student) 1 -> many Appointments
        modelBuilder.Entity<Appointment>()
            .HasOne(appointment => appointment.Student)
            .WithMany(user => user.StudentAppointments)
            .HasForeignKey(appointment => appointment.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // FacultyProfile 1 -> many Appointments
        modelBuilder.Entity<Appointment>()
            .HasOne(appointment => appointment.FacultyProfile)
            .WithMany(faculty => faculty.Appointments)
            .HasForeignKey(appointment => appointment.FacultyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // AppointmentType 1 -> many Appointments
        modelBuilder.Entity<Appointment>()
            .HasOne(appointment => appointment.AppointmentType)
            .WithMany(type => type.Appointments)
            .HasForeignKey(appointment => appointment.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // AvailabilitySlot 1 -> many Appointments
        modelBuilder.Entity<Appointment>()
            .HasOne(appointment => appointment.AvailabilitySlot)
            .WithMany(slot => slot.Appointments)
            .HasForeignKey(appointment => appointment.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Save the Status enum as text ("Pending", "Approved"...) instead of
        // numbers (0, 1...), so the table is easy to read in SSMS.
        modelBuilder.Entity<Appointment>()
            .Property(appointment => appointment.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // ----- Appointment 1 -> many Notifications -----
        // Cascade = notifications are meaningless without their appointment.
        modelBuilder.Entity<Notification>()
            .HasOne(notification => notification.Appointment)
            .WithMany(appointment => appointment.Notifications)
            .HasForeignKey(notification => notification.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
