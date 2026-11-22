using Microsoft.AspNetCore.Identity;

namespace CampusConnect.Models;

// Represents ANY person who can log in: student, faculty or admin.
//
// It inherits from IdentityUser, which already has Id, UserName, Email,
// PasswordHash, PhoneNumber and more. We only add what Identity does not have.
// Identity stores these users in the "AspNetUsers" table, so we do NOT
// create our own Users table.
//
// Whether a user is a Student, Faculty or Admin is decided by ROLES
// (set up in Phase 3), not by a column here.
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // ----- Navigation properties (links to related data) -----

    // Only faculty users have a profile, so this can be null.
    public FacultyProfile? FacultyProfile { get; set; }

    // Appointments this user booked as a STUDENT.
    public List<Appointment> StudentAppointments { get; set; } = new();
}
