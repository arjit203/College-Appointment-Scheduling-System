using CampusConnect.Data;
using CampusConnect.DTOs.Appointments;
using CampusConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CampusConnect.Controllers;

// Booking, viewing, approving/rejecting/completing and cancelling appointments.
// Base URL: /api/appointments
//
// STATUS RULES
//   Student books           -> Pending
//   Faculty:  Pending  -> Approved | Rejected
//             Approved -> Completed
//   Student:  Pending | Approved -> Cancelled
//   Rejected, Completed and Cancelled are final - nothing can change them.
//
// SLOT RULES
//   Booking marks the slot IsBooked = true.
//   Rejecting or cancelling frees the slot again (if it is still in the future).
//   Appointments are NEVER deleted, so history is always kept.
[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AppointmentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // POST /api/appointments
    // A student books one free slot.
    [HttpPost]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Book(CreateAppointmentDto request)
    {
        string studentId = GetMyUserId();

        // ----- 1. Validate everything first (simple, friendly error messages) -----

        // AsNoTracking: we only READ the slot here; we will update it later in one safe SQL statement.
        AvailabilitySlot? slot = await _context.AvailabilitySlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.AvailabilitySlotId);

        if (slot == null)
        {
            return NotFound(new { message = $"Availability slot {request.AvailabilitySlotId} was not found." }); // 404
        }

        bool facultyExists = await _context.FacultyProfiles.AnyAsync(f => f.Id == slot.FacultyProfileId);
        if (!facultyExists)
        {
            return BadRequest(new { message = "This slot does not belong to an existing faculty member." }); // 400
        }

        if (slot.StartTime <= DateTime.Now)
        {
            return BadRequest(new { message = "This slot is in the past and can no longer be booked." }); // 400
        }

        if (slot.IsBooked)
        {
            return Conflict(new { message = "Sorry, this slot has already been booked." }); // 409
        }

        bool typeExists = await _context.AppointmentTypes.AnyAsync(t => t.Id == request.AppointmentTypeId);
        if (!typeExists)
        {
            return BadRequest(new { message = $"Appointment type {request.AppointmentTypeId} does not exist." }); // 400
        }

        // ----- 2. Book inside a database TRANSACTION -----
        // Either BOTH changes are saved (slot booked + appointment created) or NEITHER is.
        // "await using" = if we leave this method without calling CommitAsync
        // (an error, or an early return), the transaction is rolled back automatically.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Re-check AND claim the slot in ONE SQL statement:
        //     UPDATE AvailabilitySlots SET IsBooked = 1 WHERE Id = @id AND IsBooked = 0
        // SQL Server locks the row while updating it. If two students book at the same
        // moment, the second UPDATE waits, then sees IsBooked = 1 and changes 0 rows.
        // So exactly ONE student can win the slot - even at the same millisecond.
        int slotsClaimed = await _context.AvailabilitySlots
            .Where(s => s.Id == slot.Id && !s.IsBooked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsBooked, true));

        if (slotsClaimed == 0)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "Sorry, this slot was just booked by someone else." }); // 409
        }

        var appointment = new Appointment
        {
            StudentId = studentId,                      // from the token, never from the request
            FacultyProfileId = slot.FacultyProfileId,   // from the slot, never from the request
            AppointmentTypeId = request.AppointmentTypeId,
            AvailabilitySlotId = slot.Id,
            AppointmentDate = slot.StartTime,           // from the slot
            Reason = request.Reason.Trim(),
            Status = AppointmentStatus.Pending,
            CreatedAt = DateTime.Now
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();   // INSERT the appointment (still inside the transaction)

        await transaction.CommitAsync();     // make both changes permanent together

        return StatusCode(StatusCodes.Status201Created, await FindAppointmentDtoAsync(appointment.Id)); // 201
    }

    // GET /api/appointments/my-appointments
    // A student's own appointment history (newest first).
    [HttpGet("my-appointments")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> GetMyAppointments()
    {
        string studentId = GetMyUserId();

        IQueryable<Appointment> myAppointments = _context.Appointments
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.AppointmentDate);

        return Ok(await ToResponseDto(myAppointments).ToListAsync()); // 200
    }

    // GET /api/appointments/faculty/my-appointments
    // Appointments that students booked with the logged-in faculty member.
    [HttpGet("faculty/my-appointments")]
    [Authorize(Roles = Roles.Faculty)]
    public async Task<IActionResult> GetFacultyAppointments()
    {
        int? myFacultyProfileId = await GetMyFacultyProfileIdAsync();
        if (myFacultyProfileId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Your account does not have a faculty profile." });
        }

        IQueryable<Appointment> received = _context.Appointments
            .Where(a => a.FacultyProfileId == myFacultyProfileId)
            .OrderBy(a => a.AppointmentDate);

        return Ok(await ToResponseDto(received).ToListAsync()); // 200
    }

    // GET /api/appointments/5
    // Only the student who booked it, the faculty member it's with, or an Admin.
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        Appointment? appointment = await _context.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound(new { message = $"Appointment {id} was not found." }); // 404
        }

        bool isAdmin = User.IsInRole(Roles.Admin);
        bool isTheStudent = appointment.StudentId == GetMyUserId();
        bool isTheFaculty = appointment.FacultyProfileId == await GetMyFacultyProfileIdAsync();

        if (!isAdmin && !isTheStudent && !isTheFaculty)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You are not allowed to view this appointment." }); // 403
        }

        return Ok(await FindAppointmentDtoAsync(id)); // 200
    }

    // PUT /api/appointments/5/status
    // The faculty member of this appointment approves, rejects or completes it.
    [HttpPut("{id}/status")]
    [Authorize(Roles = Roles.Faculty)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateAppointmentStatusDto request)
    {
        // Include the slot, because rejecting may need to free it.
        Appointment? appointment = await _context.Appointments
            .Include(a => a.AvailabilitySlot)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound(new { message = $"Appointment {id} was not found." });
        }

        // Ownership: only the faculty member this appointment belongs to.
        if (appointment.FacultyProfileId != await GetMyFacultyProfileIdAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only update your own appointments." }); // 403
        }

        AppointmentStatus newStatus = request.Status!.Value;

        if (newStatus != AppointmentStatus.Approved
            && newStatus != AppointmentStatus.Rejected
            && newStatus != AppointmentStatus.Completed)
        {
            return BadRequest(new { message = "Faculty can only set the status to Approved, Rejected or Completed." }); // 400
        }

        if (!IsAllowedFacultyChange(appointment.Status, newStatus))
        {
            return Conflict(new
            {
                message = $"Cannot change an appointment from {appointment.Status} to {newStatus}."
            }); // 409
        }

        appointment.Status = newStatus;

        // A rejected appointment gives the slot back so another student can book it.
        if (newStatus == AppointmentStatus.Rejected)
        {
            FreeSlotIfInFuture(appointment.AvailabilitySlot);
        }

        // Appointment + slot are both tracked by the DbContext, so this ONE call
        // saves both changes in a single transaction automatically.
        await _context.SaveChangesAsync();

        return Ok(await FindAppointmentDtoAsync(id)); // 200
    }

    // PUT /api/appointments/5/cancel
    // A student cancels their OWN Pending or Approved appointment.
    [HttpPut("{id}/cancel")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Cancel(int id)
    {
        Appointment? appointment = await _context.Appointments
            .Include(a => a.AvailabilitySlot)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound(new { message = $"Appointment {id} was not found." });
        }

        if (appointment.StudentId != GetMyUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only cancel your own appointments." }); // 403
        }

        if (appointment.Status != AppointmentStatus.Pending && appointment.Status != AppointmentStatus.Approved)
        {
            return Conflict(new
            {
                message = $"This appointment is {appointment.Status} and cannot be cancelled. " +
                          "Only Pending or Approved appointments can be cancelled."
            }); // 409
        }

        appointment.Status = AppointmentStatus.Cancelled;   // we keep the record - never delete it
        FreeSlotIfInFuture(appointment.AvailabilitySlot);

        await _context.SaveChangesAsync();   // appointment + slot saved together

        return Ok(await FindAppointmentDtoAsync(id)); // 200
    }

    // =====================================================================
    // Private helper methods
    // =====================================================================

    // The status changes a faculty member is allowed to make.
    private static bool IsAllowedFacultyChange(AppointmentStatus current, AppointmentStatus next)
    {
        bool fromPending = current == AppointmentStatus.Pending
                           && (next == AppointmentStatus.Approved || next == AppointmentStatus.Rejected);

        bool fromApproved = current == AppointmentStatus.Approved
                            && next == AppointmentStatus.Completed;

        return fromPending || fromApproved;
    }

    // Make a slot bookable again - but only if it hasn't already started.
    // (Nobody can book a past slot anyway, so there's no point freeing it.)
    private static void FreeSlotIfInFuture(AvailabilitySlot slot)
    {
        if (slot.StartTime > DateTime.Now)
        {
            slot.IsBooked = false;
        }
    }

    // The logged-in user's id, from the "sub" claim of the JWT.
    private string GetMyUserId()
    {
        return User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
    }

    // The logged-in user's FacultyProfile id, or null if they are not faculty.
    private async Task<int?> GetMyFacultyProfileIdAsync()
    {
        string userId = GetMyUserId();

        return await _context.FacultyProfiles
            .Where(f => f.UserId == userId)
            .Select(f => (int?)f.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<AppointmentResponseDto?> FindAppointmentDtoAsync(int id)
    {
        IQueryable<Appointment> oneAppointment = _context.Appointments.Where(a => a.Id == id);
        return await ToResponseDto(oneAppointment).FirstOrDefaultAsync();
    }

    // Turns appointment rows into response DTOs. Written once, used everywhere.
    // EF Core turns the navigation properties (a.Student, a.FacultyProfile.User, ...)
    // into SQL JOINs, so everything comes back in ONE query.
    private static IQueryable<AppointmentResponseDto> ToResponseDto(IQueryable<Appointment> appointments)
    {
        return appointments.Select(a => new AppointmentResponseDto
        {
            Id = a.Id,
            StudentName = a.Student.FullName,
            StudentEmail = a.Student.Email ?? "",
            FacultyProfileId = a.FacultyProfileId,
            FacultyName = a.FacultyProfile.User.FullName,
            DepartmentName = a.FacultyProfile.Department.Name,
            AppointmentTypeId = a.AppointmentTypeId,
            AppointmentTypeName = a.AppointmentType.Name,
            AvailabilitySlotId = a.AvailabilitySlotId,
            AppointmentDate = a.AppointmentDate,
            StartTime = a.AvailabilitySlot.StartTime,
            EndTime = a.AvailabilitySlot.EndTime,
            Reason = a.Reason,
            Status = a.Status,
            CreatedAt = a.CreatedAt
        });
    }
}
