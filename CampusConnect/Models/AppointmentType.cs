using System.ComponentModel.DataAnnotations;

namespace CampusConnect.Models;

// The kind of meeting, e.g. "Project Guidance" (30 minutes).
// Stored in a table (not an enum) so the admin can add new types later
// without changing code.
// Table: AppointmentTypes
public class AppointmentType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DurationInMinutes { get; set; }

    // One type is used by many appointments.
    public List<Appointment> Appointments { get; set; } = new();
}
