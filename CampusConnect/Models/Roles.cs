namespace CampusConnect.Models;

// The three role names, written once so we never mistype them.
// "const" means the value is fixed at compile time, which is why it can be
// used inside attributes like [Authorize(Roles = Roles.Admin)].
public static class Roles
{
    public const string Admin = "Admin";
    public const string Faculty = "Faculty";
    public const string Student = "Student";

    // Handy list used when creating the roles at startup.
    public static readonly string[] All = { Admin, Faculty, Student };
}
