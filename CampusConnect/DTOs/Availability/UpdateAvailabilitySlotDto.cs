using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Availability;

// JSON sent to PUT /api/availability/{id} to move a slot to a new time.
// Same rules as creating: local college time, e.g. "2026-12-01T11:00:00".
public class UpdateAvailabilitySlotDto
{
    [Required(ErrorMessage = "Start time is required.")]
    public DateTime? StartTime { get; set; }

    [Required(ErrorMessage = "End time is required.")]
    public DateTime? EndTime { get; set; }
}
