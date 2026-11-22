using CampusConnect.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusConnect.Controllers;

// A tiny controller used only to check that the API and database are working.
// URL: GET /api/status
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    // ASP.NET Core passes in the ApplicationDbContext we registered in Program.cs
    // (this is Dependency Injection).
    public StatusController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        // Tries to open a connection to SQL Server. Returns true or false.
        bool databaseConnected = await _context.Database.CanConnectAsync();

        var status = new
        {
            Application = "CampusConnect",
            Status = "Running",
            DatabaseConnected = databaseConnected,
            // A simple LINQ query - counts rows in the Departments table.
            // Only run it if the database is reachable.
            DepartmentCount = databaseConnected ? await _context.Departments.CountAsync() : 0,
            ServerTime = DateTime.Now
        };

        return Ok(status); // 200 OK + JSON
    }
}
