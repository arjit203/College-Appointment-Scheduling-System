using System.ComponentModel.DataAnnotations;

namespace CampusConnect.Models;

// A short message about something that happened to an appointment,
// e.g. "Dr. Mehta approved your appointment on 1 Dec at 10:00".
// Table: Notifications
public class Notification
{
    public int Id { get; set; }

    // Which appointment this message is about.
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }   // false by default

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
