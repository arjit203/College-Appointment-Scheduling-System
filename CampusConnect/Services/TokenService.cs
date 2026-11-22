using System.Security.Claims;
using System.Text;
using CampusConnect.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CampusConnect.Services;

// Creates JWT access tokens for logged-in users.
//
// A JWT has three parts separated by dots:  HEADER.PAYLOAD.SIGNATURE
//   - Header:    which algorithm signed it (HS256)
//   - Payload:   the "claims" - facts about the user (id, email, role) + expiry
//   - Signature: proves the token was made by OUR server and not changed
//
// The signature is made with a secret key that only the server knows
// (stored in User Secrets, never in source code).
public class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public JwtTokenResult CreateToken(ApplicationUser user, IList<string> roles)
    {
        // 1. Read settings. Issuer/Audience/Expiry come from appsettings.json,
        //    the secret Key comes from User Secrets.
        string key = _configuration["Jwt:Key"]!;
        string issuer = _configuration["Jwt:Issuer"]!;
        string audience = _configuration["Jwt:Audience"]!;
        int expiryMinutes = _configuration.GetValue<int>("Jwt:ExpiryMinutes");

        // 2. Claims = the facts we put inside the token.
        //    Short standard names keep the token small and easy to read.
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),                  // "sub"   = user id
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),       // "email"
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),           // "name"
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // unique token id
        };

        // A user can have more than one role, so add one "role" claim per role.
        foreach (string role in roles)
        {
            claims.Add(new Claim("role", role));
        }

        // 3. Signing credentials: the secret key + the HMAC-SHA256 algorithm.
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        DateTime expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        // 4. Describe the token...
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,         // who created the token
            Audience = audience,     // who the token is meant for
            Expires = expiresAt,
            SigningCredentials = credentials
        };

        // 5. ...and turn it into the "xxxxx.yyyyy.zzzzz" string.
        string token = new JsonWebTokenHandler().CreateToken(tokenDescriptor);

        return new JwtTokenResult(token, expiresAt);
    }
}

// A "record" is a short way to write a small class that just holds data.
// This one carries the token text and its expiry time back to the controller.
public record JwtTokenResult(string Token, DateTime ExpiresAt);
