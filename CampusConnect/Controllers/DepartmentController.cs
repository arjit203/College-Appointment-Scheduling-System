using CampusConnect.Data;
using CampusConnect.DTOs.Departments;
using CampusConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusConnect.Controllers;

// CRUD for departments.
// Base URL: /api/departments
//
// [Authorize] on the class  -> every endpoint needs a logged-in user.
// [Authorize(Roles = Admin)] on a method -> that endpoint is Admin-only.
[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DepartmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET /api/departments
    // Any logged-in user (students need this list later).
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // LINQ: sort by name and turn each Department into a DepartmentResponseDto.
        // EF Core converts this into ONE SQL query (including the faculty count).
        List<DepartmentResponseDto> departments = await _context.Departments
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentResponseDto
            {
                Id = department.Id,
                Name = department.Name,
                Description = department.Description,
                FacultyCount = department.FacultyProfiles.Count
            })
            .ToListAsync();

        return Ok(departments); // 200
    }

    // GET /api/departments/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        DepartmentResponseDto? department = await _context.Departments
            .Where(d => d.Id == id)
            .Select(d => new DepartmentResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                FacultyCount = d.FacultyProfiles.Count
            })
            .FirstOrDefaultAsync();   // one item, or null if not found

        if (department == null)
        {
            return NotFound(new { message = $"Department with id {id} was not found." }); // 404
        }

        return Ok(department); // 200
    }

    // POST /api/departments
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CreateDepartmentDto request)
    {
        string name = request.Name.Trim();

        // Prevent duplicates. (SQL Server compares text case-insensitively by default,
        // so "computer science" and "Computer Science" count as the same.)
        bool nameExists = await _context.Departments.AnyAsync(d => d.Name == name);
        if (nameExists)
        {
            return Conflict(new { message = $"A department named '{name}' already exists." }); // 409
        }

        var department = new Department
        {
            Name = name,
            Description = request.Description?.Trim()
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();   // INSERT happens here; department.Id is now filled in

        var response = new DepartmentResponseDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            FacultyCount = 0
        };

        // 201 Created + a "Location" header pointing to GET /api/departments/{id}
        return CreatedAtAction(nameof(GetById), new { id = department.Id }, response);
    }

    // PUT /api/departments/5
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, UpdateDepartmentDto request)
    {
        // FindAsync looks up a row by its primary key.
        Department? department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            return NotFound(new { message = $"Department with id {id} was not found." });
        }

        string name = request.Name.Trim();

        // Another department (not this one) already using the new name?
        bool nameTaken = await _context.Departments.AnyAsync(d => d.Name == name && d.Id != id);
        if (nameTaken)
        {
            return Conflict(new { message = $"A department named '{name}' already exists." });
        }

        // EF Core is "tracking" this object, so changing its properties
        // is enough - SaveChangesAsync works out the UPDATE statement.
        department.Name = name;
        department.Description = request.Description?.Trim();
        await _context.SaveChangesAsync();

        var response = new DepartmentResponseDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            FacultyCount = await _context.FacultyProfiles.CountAsync(f => f.DepartmentId == id)
        };

        return Ok(response); // 200
    }

    // DELETE /api/departments/5
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        Department? department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            return NotFound(new { message = $"Department with id {id} was not found." });
        }

        // Don't delete a department that faculty members still belong to.
        // (The database would also refuse, because the foreign key uses Restrict,
        //  but checking first lets us return a friendly message.)
        int facultyCount = await _context.FacultyProfiles.CountAsync(f => f.DepartmentId == id);
        if (facultyCount > 0)
        {
            return Conflict(new
            {
                message = $"Cannot delete '{department.Name}' because {facultyCount} faculty member(s) belong to it. " +
                          "Move them to another department first."
            }); // 409
        }

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync();

        return NoContent(); // 204 - success, nothing to return
    }
}
