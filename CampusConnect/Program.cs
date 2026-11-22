var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// 1. REGISTER SERVICES
//    Everything the app needs is registered here, so ASP.NET Core
//    can create it and give it to our classes (Dependency Injection).
// ---------------------------------------------------------------

// Enables API controllers (classes inside the Controllers folder).
builder.Services.AddControllers();

// Generates the OpenAPI document (a JSON description of our API)
// at /openapi/v1.json. Swagger UI reads this document.
builder.Services.AddOpenApi();

var app = builder.Build();

// ---------------------------------------------------------------
// 2. MIDDLEWARE PIPELINE
//    Every request passes through these steps, in this order.
// ---------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    // Publishes the OpenAPI document at /openapi/v1.json
    app.MapOpenApi();

    // The Swagger test page at /swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CampusConnect API v1");
    });
}

// Redirects http:// requests to https://
app.UseHttpsRedirection();

// Makes "/" return wwwroot/index.html
app.UseDefaultFiles();

// Serves files from wwwroot (HTML, CSS, JavaScript, images)
app.UseStaticFiles();

// Checks [Authorize] attributes (login/roles are added in Phase 3)
app.UseAuthorization();

// Sends /api/... requests to the matching controller method
app.MapControllers();

app.Run();
