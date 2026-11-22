namespace CampusConnect.Models;

// The fixed list of states an appointment can be in.
// Using an enum (instead of plain strings) means a typo like "Aproved"
// is caught by the compiler.
//
// Flow:  Pending -> Approved -> Completed
//        Pending -> Rejected
//        Pending / Approved -> Cancelled
public enum AppointmentStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Completed
}
