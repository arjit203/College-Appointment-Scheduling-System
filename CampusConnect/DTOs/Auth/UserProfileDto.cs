namespace CampusConnect.DTOs.Auth;

// Safe information about a user that we can send to the browser.
// It never contains PasswordHash, SecurityStamp or other Identity internals -
// that is exactly why we use a DTO instead of returning ApplicationUser.
public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
