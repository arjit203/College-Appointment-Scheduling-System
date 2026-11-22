using CampusConnect.Models;

namespace CampusConnect.DTOs.Dashboard;

// One bar/slice of the "appointments by status" chart,
// e.g. { "status": "Pending", "count": 4 }
public class StatusCountDto
{
    public AppointmentStatus Status { get; set; }   // sent as text, e.g. "Pending"
    public int Count { get; set; }
}
