using CampusConnect.Data;
using CampusConnect.DTOs.Availability;
using CampusConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CampusConnect.Controllers;

// Faculty members publish the time slots when students can meet them.
// Base URL: /api/availability
//
// ABOUT TIME: this version uses LOCAL COLLEGE TIME everywhere.
//   - Clients send times WITHOUT "Z" or an offset, e.g. "2026-12-01T10:00:00".
//   - We compare against DateTime.Now (the server's local clock).
//   - SQL Server stores the value exactly as sent (datetime2 has no time zone).
//   Times that include "Z" or "+05:30" are rejected, so UTC and local time
//   can never get mixed up by accident.
[ApiController]
[Route("api/availability")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AvailabilityController(ApplicationDbContext context)
    {
        _context = context;
    }

    // POST /api/availability
    // A faculty member creates a slot for THEMSELVES.
    [HttpPost]
    [Authorize(Roles = Roles.Faculty)]
    public async Task<IActionResult> Create(CreateAvailabilitySlotDto request)
    {
        // Who is calling? Find their faculty profile from the token.
        int? myFacultyProfileId = await GetMyFacultyProfileIdAsync();
        if (myFacultyProfileId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Your account does not have a faculty profile." });
        }

        // [Required] already guaranteed both values exist, so ".Value" is safe.
        DateTime start = request.StartTime!.Value;
        DateTime end = request.EndTime!.Value;

        string? timeError = ValidateTimes(start, end);
        if (timeError != null)
        {
            return BadRequest(new { message = timeError }); // 400
        }

        if (await OverlapsExistingSlotAsync(myFacultyProfileId.Value, start, end, slotIdToIgnore: null))
        {
            return Conflict(new { message = "This time overlaps with one of your existing slots." }); // 409
        }

        var slot = new AvailabilitySlot
        {
            FacultyProfileId = myFacultyProfileId.Value,   // ALWAYS the caller's own profile
            StartTime = start,
            EndTime = end,
            IsBooked = false
        };

        _context.AvailabilitySlots.Add(slot);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, await FindSlotDtoAsync(slot.Id)); // 201
    }

    // GET /api/availability/my-slots
    // A faculty member sees ALL their own slots (past, future, booked or not).
    [HttpGet("my-slots")]
    [Authorize(Roles = Roles.Faculty)]
    public async Task<IActionResult> GetMySlots()
    {
        int? myFacultyProfileId = await GetMyFacultyProfileIdAsync();
        if (myFacultyProfileId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Your account does not have a faculty profile." });
        }

        IQueryable<AvailabilitySlot> mySlots = _context.AvailabilitySlots
            .Where(slot => slot.FacultyProfileId == myFacultyProfileId)
            .OrderBy(slot => slot.StartTime);

        List<AvailabilitySlotResponseDto> slots = await ToResponseDto(mySlots).ToListAsync();

        return Ok(slots); // 200
    }

    // GET /api/availability/faculty/3
    // What STUDENTS see: only FUTURE slots that are NOT booked.
    [HttpGet("faculty/{facultyId}")]
    public async Task<IActionResult> GetAvailableSlotsForFaculty(int facultyId)
    {
        bool facultyExists = await _context.FacultyProfiles.AnyAsync(f => f.Id == facultyId);
        if (!facultyExists)
        {
            return NotFound(new { message = $"Faculty member with id {facultyId} was not found." }); // 404
        }

        DateTime now = DateTime.Now;

        IQueryable<AvailabilitySlot> freeSlots = _context.AvailabilitySlots
            .Where(slot => slot.FacultyProfileId == facultyId   // this faculty member
                        && !slot.IsBooked                       // still free
                        && slot.StartTime > now)                // in the future
            .OrderBy(slot => slot.StartTime);

        List<AvailabilitySlotResponseDto> slots = await ToResponseDto(freeSlots).ToListAsync();

        return Ok(slots); // 200 (an empty list is fine - it just means no free slots)
    }

    // PUT /api/availability/5
    // The owner (or an Admin) moves a slot to a new time.
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Faculty + "," + Roles.Admin)]   // "Faculty,Admin" = either role
    public async Task<IActionResult> Update(int id, UpdateAvailabilitySlotDto request)
    {
        AvailabilitySlot? slot = await _context.AvailabilitySlots.FindAsync(id);
        if (slot == null)
        {
            return NotFound(new { message = $"Slot with id {id} was not found." }); // 404
        }

        // Ownership check: is this MY slot? (Admins may manage any slot.)
        if (!await CanManageSlotAsync(slot))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only change your own slots." }); // 403
        }

        if (slot.IsBooked)
        {
            return Conflict(new { message = "This slot is booked and cannot be changed." }); // 409
        }

        if (slot.StartTime <= DateTime.Now)
        {
            return BadRequest(new { message = "Past slots cannot be changed." }); // 400
        }

        DateTime start = request.StartTime!.Value;
        DateTime end = request.EndTime!.Value;

        string? timeError = ValidateTimes(start, end);
        if (timeError != null)
        {
            return BadRequest(new { message = timeError });
        }

        // Ignore THIS slot when checking for overlaps (it's allowed to overlap its own old time).
        if (await OverlapsExistingSlotAsync(slot.FacultyProfileId, start, end, slotIdToIgnore: slot.Id))
        {
            return Conflict(new { message = "This time overlaps with another existing slot." });
        }

        slot.StartTime = start;
        slot.EndTime = end;
        await _context.SaveChangesAsync();

        return Ok(await FindSlotDtoAsync(slot.Id)); // 200
    }

    // DELETE /api/availability/5
    // The owner (or an Admin) removes an unused slot.
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Faculty + "," + Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        AvailabilitySlot? slot = await _context.AvailabilitySlots.FindAsync(id);
        if (slot == null)
        {
            return NotFound(new { message = $"Slot with id {id} was not found." });
        }

        if (!await CanManageSlotAsync(slot))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete your own slots." });
        }

        if (slot.IsBooked)
        {
            return Conflict(new { message = "This slot is booked and cannot be deleted. The appointment must be cancelled or rejected first." });
        }

        // Even a free slot may have OLD appointments (cancelled/rejected) pointing to it.
        // Deleting it would break that history, so we refuse.
        bool hasAppointmentHistory = await _context.Appointments.AnyAsync(a => a.AvailabilitySlotId == id);
        if (hasAppointmentHistory)
        {
            return Conflict(new { message = "This slot has appointment history and cannot be deleted." });
        }

        _context.AvailabilitySlots.Remove(slot);
        await _context.SaveChangesAsync();

        return NoContent(); // 204
    }

    // =====================================================================
    // Private helper methods (used by the endpoints above, not endpoints themselves)
    // =====================================================================

    // Reads the user id from the token ("sub" claim) and finds that user's FacultyProfile.
    // Returns null if the user has no faculty profile.
    private async Task<int?> GetMyFacultyProfileIdAsync()
    {
        string? userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId == null)
        {
            return null;
        }

        // Select only the Id. The "(int?)" cast makes FirstOrDefaultAsync return
        // null (instead of 0) when no profile is found.
        return await _context.FacultyProfiles
            .Where(f => f.UserId == userId)
            .Select(f => (int?)f.Id)
            .FirstOrDefaultAsync();
    }

    // Ownership rule: Admins can manage any slot; Faculty only their own.
    private async Task<bool> CanManageSlotAsync(AvailabilitySlot slot)
    {
        if (User.IsInRole(Roles.Admin))
        {
            return true;
        }

        int? myFacultyProfileId = await GetMyFacultyProfileIdAsync();
        return myFacultyProfileId == slot.FacultyProfileId;
    }

    // Simple time rules. Returns an error message, or null if everything is fine.
    private static string? ValidateTimes(DateTime start, DateTime end)
    {
        // DateTimeKind tells us how the time was written:
        //   Unspecified = "2026-12-01T10:00:00"        (what we want: local college time)
        //   Utc         = "2026-12-01T10:00:00Z"
        //   Local       = "2026-12-01T10:00:00+05:30"  (converted by the JSON reader)
        if (start.Kind != DateTimeKind.Unspecified || end.Kind != DateTimeKind.Unspecified)
        {
            return "Please send local college time without 'Z' or a time-zone offset, e.g. 2026-12-01T10:00:00.";
        }

        if (start >= end)
        {
            return "Start time must be earlier than end time.";
        }

        if (start <= DateTime.Now)
        {
            return "Slots must be in the future.";
        }

        return null;
    }

    // Two time ranges overlap when each one starts before the other one ends:
    //     existing.Start < new.End   AND   new.Start < existing.End
    // Slots that only touch (10:00-10:30 and 10:30-11:00) do NOT overlap.
    private async Task<bool> OverlapsExistingSlotAsync(int facultyProfileId, DateTime start, DateTime end, int? slotIdToIgnore)
    {
        return await _context.AvailabilitySlots.AnyAsync(slot =>
            slot.FacultyProfileId == facultyProfileId
            && slot.Id != slotIdToIgnore
            && slot.StartTime < end
            && start < slot.EndTime);
    }

    // Loads one slot as a DTO (used after create/update).
    private async Task<AvailabilitySlotResponseDto?> FindSlotDtoAsync(int id)
    {
        IQueryable<AvailabilitySlot> oneSlot = _context.AvailabilitySlots.Where(slot => slot.Id == id);
        return await ToResponseDto(oneSlot).FirstOrDefaultAsync();
    }

    // Turns slot rows into response DTOs. Written once, used by every endpoint.
    // It only DESCRIBES the conversion - nothing runs until ToListAsync() /
    // FirstOrDefaultAsync() is called, and then it becomes part of ONE SQL query.
    private static IQueryable<AvailabilitySlotResponseDto> ToResponseDto(IQueryable<AvailabilitySlot> slots)
    {
        return slots.Select(slot => new AvailabilitySlotResponseDto
        {
            Id = slot.Id,
            FacultyProfileId = slot.FacultyProfileId,
            FacultyName = slot.FacultyProfile.User.FullName,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            // DATEDIFF(minute, StartTime, EndTime) in SQL Server
            DurationInMinutes = EF.Functions.DateDiffMinute(slot.StartTime, slot.EndTime),
            IsBooked = slot.IsBooked
        });
    }
}
