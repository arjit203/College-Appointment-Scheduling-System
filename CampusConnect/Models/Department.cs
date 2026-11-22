using System.ComponentModel.DataAnnotations;

namespace CampusConnect.Models;

// A college department, e.g. "Computer Science".
// Table: Departments
public class Department
{
    // "Id" is automatically the primary key, and SQL Server
    // generates the number (1, 2, 3...) for us.
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }   // "?" = optional (can be NULL)

    // One department has many faculty members.
    public List<FacultyProfile> FacultyProfiles { get; set; } = new();
}
