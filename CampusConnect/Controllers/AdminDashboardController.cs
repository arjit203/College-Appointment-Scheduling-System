using CampusConnect.Data;
using CampusConnect.DTOs.Dashboard;
using CampusConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusConnect.Controllers;

// Statistics for the Admin dashboard.
// Base URL: /api/admin/dashboard
//
// [Authorize(Roles = Admin)] on the CLASS protects every endpoint here:
//   no token -> 401, Student or Faculty token -> 403.
//
// Every number is counted BY THE DATABASE (SQL COUNT / GROUP BY).
// We never load whole tables into memory just to count them.
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = Roles.Admin)]
public class AdminDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(ApplicationDbContext context, ILogger<AdminDashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET /api/admin/dashboard
    // Overall totals for the top of the dashboard.
    [HttpGet]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            // Students = users who have the "Student" role.
            // AspNetUserRoles links users to roles, AspNetRoles holds the role names.
            // SQL: SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON ... WHERE r.Name = 'Student'
            int totalStudents = await _context.UserRoles
                .Join(_context.Roles,
                      userRole => userRole.RoleId,
                      role => role.Id,
                      (userRole, role) => role.Name)
                .CountAsync(roleName => roleName == Roles.Student);

            int totalFaculty = await _context.FacultyProfiles.CountAsync();       // SELECT COUNT(*) FROM FacultyProfiles
            int totalDepartments = await _context.Departments.CountAsync();      // SELECT COUNT(*) FROM Departments

            // One query counts every status at once (GROUP BY Status).
            Dictionary<AppointmentStatus, int> countByStatus = await CountAppointmentsByStatusAsync();

            var summary = new DashboardSummaryDto
            {
                TotalStudents = totalStudents,
                TotalFaculty = totalFaculty,
                TotalDepartments = totalDepartments,
                TotalAppointments = countByStatus.Values.Sum(),
                PendingAppointments = countByStatus[AppointmentStatus.Pending],
                ApprovedAppointments = countByStatus[AppointmentStatus.Approved],
                RejectedAppointments = countByStatus[AppointmentStatus.Rejected],
                CompletedAppointments = countByStatus[AppointmentStatus.Completed],
                CancelledAppointments = countByStatus[AppointmentStatus.Cancelled]
            };

            return Ok(summary); // 200
        }
        catch (Exception ex)
        {
            return DatabaseError(ex, "dashboard summary");
        }
    }

    // GET /api/admin/dashboard/recent-appointments
    // The 10 most recently CREATED appointments, newest first.
    [HttpGet("recent-appointments")]
    public async Task<IActionResult> GetRecentAppointments()
    {
        try
        {
            List<RecentAppointmentDto> recent = await _context.Appointments
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)          // tie-breaker if two have the same CreatedAt
                .Take(10)                             // SQL: SELECT TOP(10)
                .Select(a => new RecentAppointmentDto
                {
                    AppointmentId = a.Id,
                    StudentName = a.Student.FullName,
                    FacultyName = a.FacultyProfile.User.FullName,
                    DepartmentName = a.FacultyProfile.Department.Name,
                    AppointmentType = a.AppointmentType.Name,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.AvailabilitySlot.StartTime,
                    EndTime = a.AvailabilitySlot.EndTime,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return Ok(recent); // 200 (empty list if there are no appointments yet)
        }
        catch (Exception ex)
        {
            return DatabaseError(ex, "recent appointments");
        }
    }

    // GET /api/admin/dashboard/appointment-statistics
    // Appointment counts per status - ready for a pie or bar chart.
    [HttpGet("appointment-statistics")]
    public async Task<IActionResult> GetAppointmentStatistics()
    {
        try
        {
            Dictionary<AppointmentStatus, int> countByStatus = await CountAppointmentsByStatusAsync();

            var statistics = new AppointmentStatisticsDto
            {
                TotalAppointments = countByStatus.Values.Sum(),
                Statuses = countByStatus
                    .Select(pair => new StatusCountDto { Status = pair.Key, Count = pair.Value })
                    .ToList()
            };

            return Ok(statistics); // 200
        }
        catch (Exception ex)
        {
            return DatabaseError(ex, "appointment statistics");
        }
    }

    // =====================================================================
    // Private helpers
    // =====================================================================

    // Counts appointments per status with ONE database query, then makes sure
    // every status appears (with 0 if there are no appointments in that status).
    private async Task<Dictionary<AppointmentStatus, int>> CountAppointmentsByStatusAsync()
    {
        // SQL: SELECT Status, COUNT(*) FROM Appointments GROUP BY Status
        var groupedCounts = await _context.Appointments
            .GroupBy(a => a.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();

        // Start every status at 0, in the enum's order (Pending, Approved, Rejected, Cancelled, Completed)...
        var result = new Dictionary<AppointmentStatus, int>();
        foreach (AppointmentStatus status in Enum.GetValues<AppointmentStatus>())
        {
            result[status] = 0;
        }

        // ...then fill in the real counts from the database.
        foreach (var item in groupedCounts)
        {
            result[item.Status] = item.Count;
        }

        return result;
    }

    // Logs the real error for developers and returns a simple message to the caller
    // (no stack traces or database details are sent to the browser).
    private ObjectResult DatabaseError(Exception ex, string whatFailed)
    {
        _logger.LogError(ex, "Could not load {WhatFailed}", whatFailed);
        return StatusCode(StatusCodes.Status500InternalServerError,
            new { message = $"Could not load the {whatFailed} right now. Please try again later." });
    }
}
