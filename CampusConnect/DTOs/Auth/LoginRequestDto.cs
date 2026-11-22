using System.ComponentModel.DataAnnotations;

namespace CampusConnect.DTOs.Auth;

// The JSON sent to POST /api/auth/login.
public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}
