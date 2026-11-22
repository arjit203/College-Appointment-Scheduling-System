namespace CampusConnect.DTOs.Departments;

// What the API sends back for a department.
public class DepartmentResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // How many faculty members belong to this department.
    // Useful in the UI, and explains why a department can't be deleted.
    public int FacultyCount { get; set; }
}
