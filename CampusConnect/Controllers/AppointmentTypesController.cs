using CampusConnect.Data;
using CampusConnect.DTOs.AppointmentTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusConnect.Controllers;

// Read-only list of appointment types, so students know which
// AppointmentTypeId to send when booking.
// The default types are created at startup by DbSeeder.
// URL: GET /api/appointment-types
[ApiController]
[Route("api/appointment-types")]
[Authorize]
public class AppointmentTypesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AppointmentTypesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        List<AppointmentTypeResponseDto> types = await _context.AppointmentTypes
            .OrderBy(type => type.Name)
            .Select(type => new AppointmentTypeResponseDto
            {
                Id = type.Id,
                Name = type.Name,
                Description = type.Description,
                DurationInMinutes = type.DurationInMinutes
            })
            .ToListAsync();

        return Ok(types);
    }
}
