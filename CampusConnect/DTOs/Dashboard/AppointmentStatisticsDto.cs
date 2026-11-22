namespace CampusConnect.DTOs.Dashboard;

// Returned by GET /api/admin/dashboard/appointment-statistics
// "Statuses" always contains all five statuses (zeros included),
// in the same order every time - easy to feed into a chart later.
public class AppointmentStatisticsDto
{
    public int TotalAppointments { get; set; }
    public List<StatusCountDto> Statuses { get; set; } = new();
}
