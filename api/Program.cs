using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;

var builder = WebApplication.CreateBuilder(args);

// EF Core (mapping-only) over Postgres — same DB as the game server (devplan D1).
// Connection string resolves from UEQ_DB_CONNSTRING env var first (parity with the Unity
// server's config), else the "Content" connection string in appsettings.
var connString =
    Environment.GetEnvironmentVariable("UEQ_DB_CONNSTRING")
    ?? builder.Configuration.GetConnectionString("Content");
builder.Services.AddDbContext<ContentDbContext>(o => o.UseNpgsql(connString));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Allow the Angular dev server to call the API (no auth — trusted local users only, D2).
const string DevCors = "AngularDev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors(DevCors);
app.UseAuthorization();
app.MapControllers();

app.Run();
