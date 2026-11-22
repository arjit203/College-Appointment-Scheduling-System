using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Departments;

// JSON the Admin sends to PUT /api/departments/{id}
// (Same fields as Create today, but kept separate so each can change on its own later.)
public class UpdateDepartmentDto
{
    [Required(ErrorMessage = "Department name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Department name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
    public string? Description { get; set; }
}
