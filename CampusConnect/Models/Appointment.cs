using System.ComponentModel.DataAnnotations;

namespace CampusConnect.Models;

// A meeting request from a student to a faculty member.
// This is the central table of the system - it links to almost everything.
// Table: Appointments
public class Appointment
{
    public int Id { get; set; }

    // The student who booked it (AspNetUsers.Id).
    [Required]
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;

    // The faculty member the student wants to meet.
    public int FacultyProfileId { get; set; }
    public FacultyProfile FacultyProfile { get; set; } = null!;

    // What kind of meeting it is.
    public int AppointmentTypeId { get; set; }
    public AppointmentType AppointmentType { get; set; } = null!;

    // The time slot that was booked.
    public int AvailabilitySlotId { get; set; }
    public AvailabilitySlot AvailabilitySlot { get; set; } = null!;

    // Date and time of the meeting (copied from the slot when booking,
    // so the appointment history stays readable on its own).
    public DateTime AppointmentDate { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    // Every new appointment starts as Pending.
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Messages generated about this appointment ("Your appointment was approved").
    public List<Notification> Notifications { get; set; } = new();
}
