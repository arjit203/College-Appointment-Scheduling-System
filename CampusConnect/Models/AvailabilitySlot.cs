namespace CampusConnect.Models;

// A time window when a faculty member is free to meet,
// e.g. 1 Dec 2026, 10:00 – 10:30.
// Table: AvailabilitySlots
public class AvailabilitySlot
{
    public int Id { get; set; }

    // Which faculty member this slot belongs to.
    public int FacultyProfileId { get; set; }
    public FacultyProfile FacultyProfile { get; set; } = null!;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // true while a Pending/Approved appointment holds this slot.
    // Set back to false if the appointment is rejected or cancelled,
    // so another student can book it.
    public bool IsBooked { get; set; }

    // A slot can appear in several appointments over time
    // (e.g. one was cancelled, then someone else booked it).
    public List<Appointment> Appointments { get; set; } = new();
}
