namespace CampusConnect.DTOs.Faculty;

// What the API sends back for a faculty member.
// It combines data from two tables: AspNetUsers (name, email)
// and FacultyProfiles (department, designation, office) - plus the department name.
// No password hash or other Identity internals are included.
public class FacultyResponseDto
{
    public int Id { get; set; }                    // FacultyProfiles.Id - used in /api/faculty/{id}
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string? OfficeLocation { get; set; }
}
