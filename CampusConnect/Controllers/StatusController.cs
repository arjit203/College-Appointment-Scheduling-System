using Microsoft.AspNetCore.Mvc;

namespace CampusConnect.Controllers;

// A tiny controller used only to check that the API is running.
// URL: GET /api/status
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var status = new
        {
            Application = "CampusConnect",
            Status = "Running",
            ServerTime = DateTime.Now
        };

        return Ok(status); // 200 OK + JSON
    }
}
