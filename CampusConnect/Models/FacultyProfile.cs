using System.ComponentModel.DataAnnotations;

namespace CampusConnect.Models;

// Extra information for users who are faculty members.
// The login details live in ApplicationUser; the teaching details live here.
// Table: FacultyProfiles
public class FacultyProfile
{
    public int Id { get; set; }

    // Foreign key to the user (AspNetUsers.Id). Identity user ids are strings.
    // Each user can have at most ONE faculty profile (one-to-one).
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    // Foreign key to the department this faculty member belongs to.
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Designation { get; set; } = string.Empty;   // e.g. "Assistant Professor"

    [MaxLength(100)]
    public string? OfficeLocation { get; set; }                // e.g. "Block B, Room 204"

    // One faculty member publishes many time slots...
    public List<AvailabilitySlot> AvailabilitySlots { get; set; } = new();

    // ...and receives many appointment requests.
    public List<Appointment> Appointments { get; set; } = new();
}

// Note on "= null!":
// EF Core fills navigation properties like User and Department when it loads data.
// "null!" tells the compiler "don't warn me, this will be filled in".
