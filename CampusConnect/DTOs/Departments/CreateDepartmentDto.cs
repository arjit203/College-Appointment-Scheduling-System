using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Departments;

// JSON the Admin sends to POST /api/departments
public class CreateDepartmentDto
{
    [Required(ErrorMessage = "Department name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Department name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
    public string? Description { get; set; }
}
