# CampusConnect – College Appointment Scheduling System

A web application where **students** book appointments with **faculty**, faculty manage their availability and approve requests, and **admins** manage departments, users and appointments.

## Tech stack
- **Backend:** C#, ASP.NET Core Web API (.NET 11)
- **Database:** Microsoft SQL Server, Entity Framework Core, LINQ
- **Authentication:** ASP.NET Core Identity with role-based authorization (Student, Faculty, Admin)
- **Frontend:** HTML, CSS, JavaScript, Bootstrap 5 (served from `wwwroot`)
- **API testing:** Swagger UI
- **Version control:** Git & GitHub

## Project status
- [x] Phase 1 – Project skeleton, Swagger, static frontend
- [ ] Phase 2 – Database with EF Core
- [ ] Phase 3 – Authentication & roles
- [ ] Phases 4–12 – Features, frontend, polish

## Run locally

Prerequisites: .NET SDK 11, SQL Server (2022/2025, Developer or Express).

```bash
cd CampusConnect
dotnet run --launch-profile http
```

- App: http://localhost:5290/
- Swagger: http://localhost:5290/swagger

## Folder structure
```
CampusConnect/
├── Controllers/   API endpoints
├── Models/        Database entity classes
├── Data/          DbContext and seed data
├── DTOs/          Request/response objects
├── Services/      Business logic
└── wwwroot/       HTML, CSS, JavaScript
```
