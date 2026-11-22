using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Auth;

// The JSON a student sends to POST /api/auth/register.
// There is deliberately NO "Role" property: public registration
// can only ever create a Student.
//
// The [attributes] are validation rules. Because AuthController has
// [ApiController], ASP.NET Core checks them automatically and returns
// 400 Bad Request with the error messages if any rule fails.
public class RegisterRequestDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    // [Compare] checks that this value equals the Password property.
    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Password and Confirm Password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
