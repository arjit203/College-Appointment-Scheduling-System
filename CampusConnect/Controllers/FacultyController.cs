using CampusConnect.Data;
using CampusConnect.DTOs.Faculty;
using CampusConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusConnect.Controllers;

// Admin creates and manages faculty accounts. Everyone logged in can view them.
// Base URL: /api/faculty
//
// A faculty member is stored in TWO tables:
//   AspNetUsers     -> login details (email, password hash, FullName)
//   FacultyProfiles -> teaching details (department, designation, office)
// They are linked by FacultyProfiles.UserId.
[ApiController]
[Route("api/faculty")]
[Authorize]
public class FacultyController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FacultyController> _logger;

    public FacultyController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<FacultyController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    // GET /api/faculty
    // Any logged-in user - students will pick faculty from this list.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // One LINQ query that reads from FacultyProfiles, AspNetUsers and Departments.
        // EF Core turns the navigation properties (f.User, f.Department) into SQL JOINs.
        List<FacultyResponseDto> faculty = await _context.FacultyProfiles
            .OrderBy(f => f.User.FullName)
            .Select(f => new FacultyResponseDto
            {
                Id = f.Id,
                UserId = f.UserId,
                FullName = f.User.FullName,
                Email = f.User.Email ?? "",
                DepartmentId = f.DepartmentId,
                DepartmentName = f.Department.Name,
                Designation = f.Designation,
                OfficeLocation = f.OfficeLocation
            })
            .ToListAsync();

        return Ok(faculty); // 200
    }

    // GET /api/faculty/5   (5 = FacultyProfiles.Id)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        FacultyResponseDto? faculty = await FindFacultyDtoAsync(id);

        if (faculty == null)
        {
            return NotFound(new { message = $"Faculty member with id {id} was not found." }); // 404
        }

        return Ok(faculty);
    }

    // POST /api/faculty
    // Creates the login account + Faculty role + FacultyProfile.
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CreateFacultyDto request)
    {
        // ----- Checks BEFORE creating anything -----

        bool departmentExists = await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId);
        if (!departmentExists)
        {
            return BadRequest(new { message = $"Department with id {request.DepartmentId} does not exist." }); // 400
        }

        if (await _userManager.FindByEmailAsync(request.Email) != null)
        {
            return Conflict(new { message = "An account with this email already exists." }); // 409
        }

        // ----- Step 1: create the login account (AspNetUsers row) -----
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName.Trim(),
            EmailConfirmed = true      // the Admin created it, so we trust the email
        };

        // UserManager checks the password rules and HASHES the password.
        IdentityResult createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(error => error.Description).ToList();
            return BadRequest(new { message = "Could not create the faculty account.", errors }); // 400
        }

        // ----- Steps 2 & 3: give the Faculty role and create the profile -----
        // The user row already exists now. If anything below fails we must remove it,
        // otherwise we'd be left with a "half" faculty account (a login with no profile).
        try
        {
            IdentityResult roleResult = await _userManager.AddToRoleAsync(user, Roles.Faculty);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("Could not assign the Faculty role.");
            }

            var profile = new FacultyProfile
            {
                UserId = user.Id,
                DepartmentId = request.DepartmentId,
                Designation = request.Designation.Trim(),
                OfficeLocation = request.OfficeLocation?.Trim()
            };

            _context.FacultyProfiles.Add(profile);
            await _context.SaveChangesAsync();

            FacultyResponseDto? response = await FindFacultyDtoAsync(profile.Id);
            return CreatedAtAction(nameof(GetById), new { id = profile.Id }, response); // 201
        }
        catch (Exception ex)
        {
            // Log the real error for developers (never sent to the user).
            _logger.LogError(ex, "Creating faculty profile failed for {Email}. Removing the user account.", request.Email);

            // The failed FacultyProfile is still waiting in EF Core's list of pending changes.
            // Clear that list first, otherwise DeleteAsync would try to save it again.
            _context.ChangeTracker.Clear();
            await _userManager.DeleteAsync(user);   // also removes the role link (cascade)

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Could not create the faculty account. No changes were saved." }); // 500
        }
    }

    // PUT /api/faculty/5
    // Admin can change name, department, designation and office location.
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, UpdateFacultyDto request)
    {
        // Include(f => f.User) also loads the linked ApplicationUser,
        // because FullName lives in AspNetUsers, not in FacultyProfiles.
        FacultyProfile? profile = await _context.FacultyProfiles
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (profile == null)
        {
            return NotFound(new { message = $"Faculty member with id {id} was not found." });
        }

        bool departmentExists = await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId);
        if (!departmentExists)
        {
            return BadRequest(new { message = $"Department with id {request.DepartmentId} does not exist." });
        }

        // Both objects are tracked by the same DbContext, so ONE SaveChangesAsync
        // updates both tables together.
        profile.User.FullName = request.FullName.Trim();
        profile.DepartmentId = request.DepartmentId;
        profile.Designation = request.Designation.Trim();
        profile.OfficeLocation = request.OfficeLocation?.Trim();

        await _context.SaveChangesAsync();

        return Ok(await FindFacultyDtoAsync(id)); // 200
    }

    // DELETE /api/faculty/5
    // Deletes the faculty account ONLY if nothing important depends on it.
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        FacultyProfile? profile = await _context.FacultyProfiles
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (profile == null)
        {
            return NotFound(new { message = $"Faculty member with id {id} was not found." });
        }

        // Appointment history must never be lost.
        int appointmentCount = await _context.Appointments.CountAsync(a => a.FacultyProfileId == id);
        if (appointmentCount > 0)
        {
            return Conflict(new
            {
                message = $"Cannot delete {profile.User.FullName} because they have {appointmentCount} appointment(s) in the system. " +
                          "Appointment history must be kept."
            }); // 409
        }

        // Slots would be deleted automatically (cascade), but we ask the Admin
        // to remove them on purpose so nothing disappears by accident.
        int slotCount = await _context.AvailabilitySlots.CountAsync(s => s.FacultyProfileId == id);
        if (slotCount > 0)
        {
            return Conflict(new
            {
                message = $"Cannot delete {profile.User.FullName} because they still have {slotCount} availability slot(s). " +
                          "Remove the slots first."
            }); // 409
        }

        // Deleting the user also deletes the FacultyProfile (cascade from Phase 2)
        // and the user's role links in AspNetUserRoles.
        IdentityResult result = await _userManager.DeleteAsync(profile.User);
        if (!result.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Could not delete the faculty account." });
        }

        return NoContent(); // 204
    }

    // Helper used by several endpoints: loads one faculty member as a DTO.
    // "private" = only this controller can use it; it is not an API endpoint.
    private async Task<FacultyResponseDto?> FindFacultyDtoAsync(int id)
    {
        return await _context.FacultyProfiles
            .Where(f => f.Id == id)
            .Select(f => new FacultyResponseDto
            {
                Id = f.Id,
                UserId = f.UserId,
                FullName = f.User.FullName,
                Email = f.User.Email ?? "",
                DepartmentId = f.DepartmentId,
                DepartmentName = f.Department.Name,
                Designation = f.Designation,
                OfficeLocation = f.OfficeLocation
            })
            .FirstOrDefaultAsync();
    }
}
