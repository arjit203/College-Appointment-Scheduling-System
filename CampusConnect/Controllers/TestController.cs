using CampusConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusConnect.Controllers;

// Simple endpoints that only exist to DEMONSTRATE role-based authorization.
// Base URL: /api/test
//
// [Authorize] on the class = every endpoint here needs a valid token.
//   No token / bad token        -> 401 Unauthorized
//   Valid token, wrong role     -> 403 Forbidden
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TestController : ControllerBase
{
    // GET /api/test/student
    [HttpGet("student")]
    [Authorize(Roles = Roles.Student)]
    public IActionResult StudentOnly()
    {
        return Ok(new { message = $"Hello {User.Identity?.Name}, you are a Student." });
    }

    // GET /api/test/faculty
    [HttpGet("faculty")]
    [Authorize(Roles = Roles.Faculty)]
    public IActionResult FacultyOnly()
    {
        return Ok(new { message = $"Hello {User.Identity?.Name}, you are Faculty." });
    }

    // GET /api/test/admin
    [HttpGet("admin")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult AdminOnly()
    {
        return Ok(new { message = $"Hello {User.Identity?.Name}, you are an Admin." });
    }
}
