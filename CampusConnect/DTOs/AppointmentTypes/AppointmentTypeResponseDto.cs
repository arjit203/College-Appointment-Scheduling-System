namespace CampusConnect.DTOs.AppointmentTypes;

// One appointment type, e.g. { "id": 2, "name": "Project Guidance", "durationInMinutes": 30 }
public class AppointmentTypeResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationInMinutes { get; set; }
}
