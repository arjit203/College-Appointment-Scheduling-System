using CampusConnect.Models;

namespace CampusConnect.DTOs.Dashboard;

// One row of the "recent appointments" table on the Admin dashboard.
// Only names are included - no emails, ids of users, or Identity fields.
public class RecentAppointmentDto
{
    public int AppointmentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string FacultyName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }     // local college time
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AppointmentStatus Status { get; set; }     // sent as text
    public DateTime CreatedAt { get; set; }
}
