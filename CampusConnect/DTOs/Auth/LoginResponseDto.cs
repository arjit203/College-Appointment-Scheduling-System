namespace CampusConnect.DTOs.Auth;

// What the API sends back after a successful login.
// The frontend stores "Token" and sends it with every protected request:
//     Authorization: Bearer <token>
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }          // UTC time when the token stops working
    public UserProfileDto User { get; set; } = new();
}
