using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Appointments;

// JSON a student sends to POST /api/appointments
//
// Only three things come from the student. Everything else is decided by the server:
//   StudentId        -> from the login token (so nobody can book as someone else)
//   FacultyProfileId -> from the chosen slot (so nobody can swap the faculty member)
//   AppointmentDate  -> from the chosen slot
//   Status           -> always starts as Pending
public class CreateAppointmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Please choose a valid availability slot.")]
    public int AvailabilitySlotId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a valid appointment type.")]
    public int AppointmentTypeId { get; set; }

    [Required(ErrorMessage = "Please tell the faculty member why you want to meet.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
