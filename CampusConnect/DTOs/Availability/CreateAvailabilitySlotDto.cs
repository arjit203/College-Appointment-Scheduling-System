using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Availability;

// JSON a faculty member sends to POST /api/availability
//
// Notice there is NO FacultyProfileId here. The server works out which
// faculty member is creating the slot from their login token, so nobody
// can create slots for someone else.
//
// TIME FORMAT: local college time, without "Z" or a time-zone offset.
// Example: "2026-12-01T10:00:00"
public class CreateAvailabilitySlotDto
{
    [Required(ErrorMessage = "Start time is required.")]
    public DateTime? StartTime { get; set; }

    [Required(ErrorMessage = "End time is required.")]
    public DateTime? EndTime { get; set; }
}

// Why "DateTime?" (nullable)?
// A plain DateTime can never be null - if the JSON leaves it out it silently
// becomes 01-01-0001. Making it nullable lets [Required] detect a missing value.
