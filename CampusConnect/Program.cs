using System.Text;
using CampusConnect.Data;
using CampusConnect.Models;
using CampusConnect.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// 1. REGISTER SERVICES
//    Everything the app needs is registered here, so ASP.NET Core
//    can create it and give it to our classes (Dependency Injection).
// ---------------------------------------------------------------

// ----- Database -----
// Reads "ConnectionStrings:DefaultConnection" from appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// Registers our database context so it can be injected into services/controllers.
// A new ApplicationDbContext is created for every HTTP request (Scoped lifetime).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ----- ASP.NET Core Identity (users, passwords, roles) -----
// AddIdentityCore = the user/password/role features WITHOUT login cookies,
// because we use JWT tokens instead.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Password rules
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // No two accounts with the same email
        options.User.RequireUniqueEmail = true;

        // Lock the account for 5 minutes after 5 wrong passwords
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()                              // enables RoleManager (Admin/Faculty/Student)
    .AddEntityFrameworkStores<ApplicationDbContext>()      // save users/roles in our SQL Server database
    .AddSignInManager();                                   // used to check passwords with lockout

// ----- JWT authentication -----
// The secret signing key is read from User Secrets (development) or an
// environment variable (production). It is never written in source code.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or too short (min 32 characters). " +
        "Set it with: dotnet user-secrets set \"Jwt:Key\" \"<a long random string>\"");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)   // "Bearer" is the default way to authenticate
    .AddJwtBearer(options =>
    {
        // Keep claim names exactly as we wrote them ("sub", "email", "role")
        // instead of converting them to long Microsoft URLs.
        options.MapInboundClaims = false;

        // Rules every incoming token must pass:
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                              // created by CampusConnect?
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,                            // meant for CampusConnect users?
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,                            // not expired?
            ValidateIssuerSigningKey = true,                    // signature made with OUR key?
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            NameClaimType = "name",   // User.Identity.Name  = the "name" claim
            RoleClaimType = "role"    // [Authorize(Roles=...)] looks at the "role" claim
        };
    });

builder.Services.AddAuthorization();

// ----- Our own services -----
builder.Services.AddScoped<TokenService>();

// Enables API controllers (classes inside the Controllers folder).
builder.Services.AddControllers();

// Turns unexpected errors into a clean JSON response (no stack traces for users).
builder.Services.AddProblemDetails();

// Generates the OpenAPI document (a JSON description of our API) at /openapi/v1.json.
// The transformer adds a "Bearer" security scheme, which makes Swagger show
// the green "Authorize" button where you paste your JWT token.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token from /api/auth/login (without the word 'Bearer')."
        };

        // Send the token with every request made from Swagger.
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });

        return Task.CompletedTask;
    });
});

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    // In production, hide error details from users.
    app.UseExceptionHandler();
}

// Enable OpenAPI and Swagger in both Development and Production.
app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "CampusConnect API v1");
    options.EnablePersistAuthorization();
});

if (app.Environment.IsDevelopment())
{
    // Publishes the OpenAPI document at /openapi/v1.json
    app.MapOpenApi();

    // The Swagger test page at /swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CampusConnect API v1");
        options.EnablePersistAuthorization();   // remember the token after a page refresh
    });
}
else
{
    // In production, hide error details from users.
    app.UseExceptionHandler();
}

// Redirects http:// requests to https://
app.UseHttpsRedirection();

// Makes "/" return wwwroot/index.html
app.UseDefaultFiles();

// Serves files from wwwroot (HTML, CSS, JavaScript, images)
app.UseStaticFiles();

// WHO are you? Reads the "Authorization: Bearer <token>" header,
// validates the token and fills in "User" with its claims.
app.UseAuthentication();

// ARE YOU ALLOWED? Checks [Authorize] / [Authorize(Roles = ...)].
// Must come AFTER UseAuthentication.
app.UseAuthorization();

// Sends /api/... requests to the matching controller method
app.MapControllers();

app.Run();
