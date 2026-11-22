namespace CampusConnect.DTOs.Availability;

// What the API sends back for a slot.
public class AvailabilitySlotResponseDto
{
    public int Id { get; set; }
    public int FacultyProfileId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }        // local college time
    public DateTime EndTime { get; set; }          // local college time
    public int DurationInMinutes { get; set; }
    public bool IsBooked { get; set; }
}
