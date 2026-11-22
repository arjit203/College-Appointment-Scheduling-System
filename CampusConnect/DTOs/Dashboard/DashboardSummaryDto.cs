namespace CampusConnect.DTOs.Dashboard;

// The numbers shown at the top of the Admin dashboard.
// Returned by GET /api/admin/dashboard
public class DashboardSummaryDto
{
    public int TotalStudents { get; set; }
    public int TotalFaculty { get; set; }
    public int TotalDepartments { get; set; }

    public int TotalAppointments { get; set; }
    public int PendingAppointments { get; set; }
    public int ApprovedAppointments { get; set; }
    public int RejectedAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
}
