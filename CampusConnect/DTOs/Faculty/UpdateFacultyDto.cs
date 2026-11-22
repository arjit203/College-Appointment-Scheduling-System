using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Faculty;

// JSON the Admin sends to PUT /api/faculty/{id}
// Email and password are NOT changed here - only profile information.
public class UpdateFacultyDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a valid department.")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Designation is required.")]
    [StringLength(100, ErrorMessage = "Designation cannot be longer than 100 characters.")]
    public string Designation { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Office location cannot be longer than 100 characters.")]
    public string? OfficeLocation { get; set; }
}
