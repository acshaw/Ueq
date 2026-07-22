using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ueq.ContentApi.Auth;
using Ueq.ContentApi.Data;

var builder = WebApplication.CreateBuilder(args);

// EF Core (mapping-only) over Postgres — same DB as the game server (devplan D1).
// Connection string resolves from UEQ_DB_CONNSTRING env var first (parity with the Unity
// server's config), else the "Content" connection string in appsettings.
var connString =
    Environment.GetEnvironmentVariable("UEQ_DB_CONNSTRING")
    ?? builder.Configuration.GetConnectionString("Content");
builder.Services.AddDbContext<ContentDbContext>(o => o.UseNpgsql(connString));

// Every controller requires a valid session by default (5.11) — a future new controller can't
// accidentally ship unprotected. Auth endpoints themselves opt out via [AllowAnonymous].
builder.Services.AddControllers(o => o.Filters.Add(new AuthorizeFilter()));
builder.Services.AddOpenApi();

// Session = a JWT in an HttpOnly cookie, not an Authorization header — read it out of the
// cookie via OnMessageReceived since JwtBearer expects a header by default.
var jwtSecret = AuthConfig.JwtSecret(builder.Environment);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue("ueq_session", out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// Allow the Angular dev server to call the API, with the session cookie included (5.11 —
// AllowCredentials needed now that requests carry auth; a specific origin, not AllowAnyOrigin,
// since credentialed CORS requires one).
const string DevCors = "AngularDev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors(DevCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
