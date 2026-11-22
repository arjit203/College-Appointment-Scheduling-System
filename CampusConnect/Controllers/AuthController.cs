using CampusConnect.DTOs.Auth;
using CampusConnect.Models;
using CampusConnect.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CampusConnect.Controllers;

// Handles registration, login and "who am I?".
// Base URL: /api/auth
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // These are provided by ASP.NET Core Identity (registered in Program.cs).
    private readonly UserManager<ApplicationUser> _userManager;     // create/find users, roles, passwords
    private readonly SignInManager<ApplicationUser> _signInManager; // checks passwords + lockout
    private readonly TokenService _tokenService;                    // our class that builds JWTs

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    // POST /api/auth/register
    // Creates a new STUDENT account. Nobody can choose another role here.
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request)
    {
        // (Required fields, email format and password match were already
        //  checked automatically using the attributes in RegisterRequestDto.)

        // 1. Stop duplicate accounts.
        if (await _userManager.FindByEmailAsync(request.Email) != null)
        {
            return Conflict(new { message = "An account with this email already exists." }); // 409
        }

        // 2. Build the user. We use the email as the username too.
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName.Trim()
        };

        // 3. Save it. Identity HASHES the password - the plain password is never stored.
        IdentityResult result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // e.g. "Passwords must have at least one uppercase ('A'-'Z')."
            var errors = result.Errors.Select(error => error.Description).ToList();
            return BadRequest(new { message = "Registration failed.", errors }); // 400
        }

        // 4. Every public registration becomes a Student - decided by the SERVER.
        await _userManager.AddToRoleAsync(user, Roles.Student);

        var profile = new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = new List<string> { Roles.Student }
        };

        return StatusCode(StatusCodes.Status201Created, profile); // 201
    }

    // POST /api/auth/login
    // Checks email + password and returns a JWT token.
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Same message for "no such email" and "wrong password", so an attacker
        // cannot use this endpoint to discover which emails are registered.
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password." }); // 401
        }

        // Checks the password against the stored hash.
        // lockoutOnFailure: true -> after 5 wrong attempts the account is locked for 5 minutes.
        // (This does NOT create a cookie - it only checks the password.)
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Unauthorized(new { message = "Account locked after too many failed attempts. Try again in 5 minutes." });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Password is correct -> build the token with the user's roles inside it.
        IList<string> roles = await _userManager.GetRolesAsync(user);
        JwtTokenResult token = _tokenService.CreateToken(user, roles);

        var response = new LoginResponseDto
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Roles = roles.ToList()
            }
        };

        return Ok(response); // 200
    }

    // GET /api/auth/me
    // Returns the details of whoever owns the token. Any logged-in user may call it.
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        // "User" is filled in by the JWT middleware from the token's claims.
        // "sub" is the claim where we stored the user's id.
        string? userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (userId == null)
        {
            return Unauthorized();
        }

        // Load fresh data from the database (the token could be up to 60 minutes old).
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User no longer exists." }); // 404
        }

        var profile = new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Roles = (await _userManager.GetRolesAsync(user)).ToList()
        };

        return Ok(profile);
    }

    // Note about LOGOUT:
    // With JWT there is nothing to "switch off" on the server - the token is
    // stored by the client (browser). Logging out simply means the frontend
    // deletes its stored token. The token also expires on its own after 60 minutes.
}
