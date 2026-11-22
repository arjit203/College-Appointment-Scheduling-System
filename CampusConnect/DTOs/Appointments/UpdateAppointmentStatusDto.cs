using System.ComponentModel.DataAnnotations;
using CampusConnect.Models;

namespace CampusConnect.DTOs.Appointments;

// JSON a faculty member sends to PUT /api/appointments/{id}/status
// Example: { "status": "Approved" }
//
// Faculty may only choose Approved, Rejected or Completed.
// (Students use the separate /cancel endpoint to cancel.)
public class UpdateAppointmentStatusDto
{
    // Nullable so [Required] can detect a missing value.
    // Thanks to JsonStringEnumConverter (Program.cs) the JSON uses words like "Approved".
    // An unknown word such as "Done" is rejected automatically with 400.
    [Required(ErrorMessage = "Status is required (Approved, Rejected or Completed).")]
    public AppointmentStatus? Status { get; set; }
}
