# CampusConnect – College Appointment Scheduling System

## 1. Introduction

CampusConnect is a web application that lets college **students book meetings with faculty members** without walking to their cabins and hoping they are free.

- **Faculty** publish the time slots when they are available and approve or reject requests.
- **Students** find a faculty member, pick a free slot, book it and track the status.
- **Admins** manage departments and faculty accounts and watch overall statistics.

It is a single ASP.NET Core project: a **REST Web API** (C#) plus a **plain HTML/CSS/JavaScript frontend** served from the same project, backed by **SQL Server**.

## 2. Main features

| Role | Features |
|------|----------|
| **Student** | Self-registration · browse faculty · search by name · filter by department · view a faculty member's free slots · book an appointment (type + reason) · appointment history with colour-coded statuses · cancel Pending/Approved appointments |
| **Faculty** | Overview counts and upcoming meetings · add / edit / delete availability slots (booked and past slots are locked) · view requests with student name and email · approve / reject Pending requests · mark Approved appointments as Completed |
| **Admin** | Live dashboard (students, faculty, departments, appointments per status) · appointments-by-status breakdown · 10 most recent appointments · department CRUD · faculty account CRUD (creates the login + profile) |
| **Everyone** | JWT login, role-based redirect, logout, server-side role and ownership checks on every API |

Business rules enforced by the server:
- A slot can only be booked by **one** student (atomic database update inside a transaction – no double booking, even with simultaneous clicks).
- Slots must be in the future, end after they start, and may not overlap the same faculty member's other slots.
- Status flow: `Pending → Approved → Completed`, `Pending → Rejected`, `Pending/Approved → Cancelled`. Rejecting or cancelling frees the slot.
- Appointments are never deleted (history is kept). Departments with faculty, faculty with appointments/slots, and booked slots cannot be deleted.

## 3. Technologies

| Layer | Technology |
|-------|-----------|
| Backend | C#, ASP.NET Core Web API (.NET 11) |
| Database | Microsoft SQL Server, Entity Framework Core 11 (code-first migrations), LINQ |
| Security | ASP.NET Core Identity (password hashing, roles), JWT Bearer authentication |
| Frontend | HTML5, CSS3, JavaScript (no framework), Bootstrap 5.3 (CDN) |
| API testing | Swagger UI (OpenAPI) |
| Version control | Git & GitHub |

## 4. Project folder structure

```
College Appointment Scheduling System/
├── README.md
├── .gitignore
└── CampusConnect/                      ASP.NET Core project
    ├── Program.cs                      Startup: services, JWT, Identity, Swagger, middleware, seeding
    ├── appsettings.json                Connection string + non-secret JWT settings
    ├── Controllers/                    API endpoints
    │   ├── AuthController.cs             register / login / me
    │   ├── DepartmentController.cs       /api/departments
    │   ├── FacultyController.cs          /api/faculty
    │   ├── AvailabilityController.cs     /api/availability
    │   ├── AppointmentsController.cs     /api/appointments
    │   ├── AppointmentTypesController.cs /api/appointment-types
    │   ├── AdminDashboardController.cs   /api/admin/dashboard
    │   ├── StatusController.cs           /api/status (health check)
    │   └── TestController.cs             /api/test/* (role-authorization demo)
    ├── Models/                         Database entities (+ AppointmentStatus enum, Roles constants)
    ├── Data/                           ApplicationDbContext + DbSeeder (roles, first admin, appointment types)
    ├── DTOs/                           Request/response classes per feature
    ├── Services/TokenService.cs        Creates JWT tokens
    ├── Migrations/                     EF Core migrations (InitialCreate, AddUniqueDepartmentName)
    └── wwwroot/                        Frontend
        ├── index.html                    Login
        ├── register.html                 Student registration
        ├── dashboard.html                Redirects to the right dashboard
        ├── admin-dashboard.html / faculty-dashboard.html / student-dashboard.html
        ├── css/style.css
        └── js/
            ├── api.js                    fetch wrapper (JWT header, errors), session, shared UI helpers
            ├── auth.js                   login + registration
            ├── dashboard.js              page guard, navbar, sidebar, logout
            └── admin.js / faculty.js / student.js   one script per dashboard
```

Architecture: **Controllers → ApplicationDbContext (EF Core) → SQL Server**, with DTOs at the API boundary. No repository pattern, CQRS or extra layers – kept deliberately simple.

## 5. Database

Seven entities (`AspNetUsers` comes from ASP.NET Core Identity and is extended by `ApplicationUser`):

| Table | Purpose | Key relationships |
|-------|---------|-------------------|
| AspNetUsers (`ApplicationUser`) | Every person who can log in (+ `FullName`) | roles via AspNetUserRoles |
| Departments | College departments (unique name) | 1 → many FacultyProfiles |
| FacultyProfiles | Faculty details (designation, office) | 1 ↔ 1 AspNetUsers, many → 1 Department |
| AppointmentTypes | e.g. Project Guidance (30 min) | 1 → many Appointments |
| AvailabilitySlots | A faculty member's free time window | many → 1 FacultyProfile |
| Appointments | A student's booking | → Student (AspNetUsers), FacultyProfile, AppointmentType, AvailabilitySlot |
| Notifications | Reserved for messages about appointments | many → 1 Appointment |

Plus Identity's `AspNetRoles`, `AspNetUserRoles` and helper tables, and `__EFMigrationsHistory`.

Delete rules: foreign keys **into Appointments are Restrict** (history is protected); a user's FacultyProfile and a profile's slots cascade; departments with faculty are Restrict.

### Database setup
1. Install **SQL Server** (Express or Developer edition) and, optionally, **SQL Server Management Studio**.
2. Install the EF Core tool once: `dotnet tool install --global dotnet-ef`
3. Create the database from the migrations:
   ```bash
   cd CampusConnect
   dotnet ef database update
   ```
   This creates **CampusConnectDb** with all tables.

### Connection string
In `CampusConnect/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=CampusConnectDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```
- `Server=localhost` = default SQL Server instance on this PC with **Windows Authentication** (no password stored).
- For a named instance use e.g. `Server=localhost\\SQLEXPRESS` (the backslash is doubled inside JSON).

## 6. Secrets and the first Admin account

Secrets are **never** stored in the repository. In development they live in .NET **User Secrets** (outside the project folder). Set them once:

```bash
cd CampusConnect
dotnet user-secrets set "Jwt:Key" "<a random string of at least 32 characters>"
dotnet user-secrets set "SeedAdmin:Email" "<admin email>"
dotnet user-secrets set "SeedAdmin:Password" "<strong password: 8+ chars, upper, lower, digit>"
dotnet user-secrets set "SeedAdmin:FullName" "System Administrator"
```

On startup the app automatically creates the roles **Admin, Faculty, Student**, the four default **appointment types**, and the **Admin account** from these secrets (only if they don't exist yet). If `Jwt:Key` is missing the app stops with a clear message. In production, use environment variables instead (`Jwt__Key`, `SeedAdmin__Email`, …).

## 7. Running the application

```bash
cd CampusConnect
dotnet run --launch-profile http
```

| What | URL |
|------|-----|
| Application (login page) | http://localhost:5290/ |
| Swagger (API testing, Development only) | http://localhost:5290/swagger |

Stop with **Ctrl + C**. After changing a `.js`/`.css` file, refresh the browser with **Ctrl + F5**.

## 8. Testing each role

Accounts:
- **Admin** – the account defined in User Secrets (`dotnet user-secrets list` shows it).
- **Faculty** – created by the Admin (Admin → Faculty → *Add faculty member*); log in with the email and starting password the Admin chose.
- **Student** – anyone can register on the *Create an account* page.

### Admin
1. Log in → *Overview*: statistics cards, appointments by status, recent appointments.
2. *Departments*: add one (try a duplicate name → error), edit it, delete an unused one (a department with faculty can't be deleted).
3. *Faculty*: add a faculty member (choose a department), edit, delete (accounts with appointments/slots are protected).

### Faculty
1. Log in → *My availability*: add a few slots for a future date (overlapping or end-before-start slots are rejected), edit and delete a free slot.
2. After a student books: *Appointments* → Approve / Reject Pending requests, *Mark completed* on Approved ones. Rejecting frees the slot.
3. *Overview* shows the counts and upcoming approved meetings.

### Student
1. *Create an account* → log in.
2. *Find faculty*: search by name, filter by department → *View slots & book* → choose a slot, a type, enter a reason → *Confirm booking*.
3. *My appointments*: see the status (Pending/Approved/Rejected/Completed/Cancelled) and cancel a Pending or Approved one – the slot becomes bookable again.

### Security checks you can demonstrate
- Open `admin-dashboard.html` as a student → redirected to the student dashboard.
- In Swagger, call `GET /api/admin/dashboard` with a student token → **403**; without a token → **401**.
- Try booking a slot that is already booked → **409** "already booked".

## 9. API overview

| Area | Endpoints |
|------|-----------|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Departments | `GET/POST /api/departments`, `GET/PUT/DELETE /api/departments/{id}` (write = Admin) |
| Faculty | `GET/POST /api/faculty`, `GET/PUT/DELETE /api/faculty/{id}` (write = Admin) |
| Availability | `POST /api/availability`, `GET /api/availability/my-slots` (Faculty), `GET /api/availability/faculty/{facultyId}` (free future slots), `PUT/DELETE /api/availability/{id}` (owner or Admin) |
| Appointments | `POST /api/appointments`, `GET /api/appointments/my-appointments` (Student), `GET /api/appointments/faculty/my-appointments` (Faculty), `GET /api/appointments/{id}` (owner/faculty/Admin), `PUT /api/appointments/{id}/status` (Faculty), `PUT /api/appointments/{id}/cancel` (Student) |
| Lookup | `GET /api/appointment-types` |
| Admin dashboard | `GET /api/admin/dashboard`, `.../recent-appointments`, `.../appointment-statistics` |

All times are **local college time** in the format `2026-12-01T10:00:00` (no `Z`/offset).

## 10. Known limitations
- No email or in-app notifications yet (the `Notifications` table is reserved for this).
- "Completed" can be set any time after approval (not only after the meeting time).
- The JWT is stored in the browser's localStorage and expires after 60 minutes; there are no refresh tokens.
- Bootstrap is loaded from a CDN, so the pages need internet access for styling.
- Single time zone (local college time).
