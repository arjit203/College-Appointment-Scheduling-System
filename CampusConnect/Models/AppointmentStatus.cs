using System.Text.Json.Serialization;

namespace CampusConnect.Models;

// The fixed list of states an appointment can be in.
// Using an enum (instead of plain strings) means a typo like "Aproved"
// is caught by the compiler.
//
// Flow:  Pending -> Approved -> Completed
//        Pending -> Rejected
//        Pending / Approved -> Cancelled
//
// [JsonConverter(...)] makes the API send and receive these as WORDS
// ("status": "Approved") instead of numbers ("status": 1). Swagger also
// shows the allowed words in its drop-down.
// (In the database they are already stored as text - see ApplicationDbContext.)
[JsonConverter(typeof(JsonStringEnumConverter<AppointmentStatus>))]
public enum AppointmentStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Completed
}
