using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Faculty;

// JSON the Admin sends to POST /api/faculty
// Creates BOTH a login account (AspNetUsers) and a FacultyProfiles row.
public class CreateFacultyDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    // The Admin sets a starting password and gives it to the faculty member.
    // Identity hashes it - it is never stored as plain text.
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    // [Range] makes sure a real id is sent (0 or negative numbers are rejected).
    [Range(1, int.MaxValue, ErrorMessage = "Please choose a valid department.")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Designation is required.")]
    [StringLength(100, ErrorMessage = "Designation cannot be longer than 100 characters.")]
    public string Designation { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Office location cannot be longer than 100 characters.")]
    public string? OfficeLocation { get; set; }
}
