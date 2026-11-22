using CampusConnect.Models;

namespace CampusConnect.DTOs.Appointments;

// What the API sends back for an appointment.
// Combines five tables: Appointments, AspNetUsers (student), FacultyProfiles + AspNetUsers (faculty),
// Departments, AppointmentTypes and AvailabilitySlots.
// Only names and the student's email are shared - no password hashes or other Identity data.
public class AppointmentResponseDto
{
    public int Id { get; set; }

    // Student
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;

    // Faculty
    public int FacultyProfileId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;

    // What and when
    public int AppointmentTypeId { get; set; }
    public string AppointmentTypeName { get; set; } = string.Empty;
    public int AvailabilitySlotId { get; set; }
    public DateTime AppointmentDate { get; set; }     // local college time (same as StartTime)
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }     // sent as text, e.g. "Pending"
    public DateTime CreatedAt { get; set; }
}
