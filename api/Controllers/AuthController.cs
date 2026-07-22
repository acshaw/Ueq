using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ueq.ContentApi.Auth;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

public record RegisterRequest(string Username, string Password, string InviteCode);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string Username);

/// <summary>
/// Web-admin auth (5.11) — gates the content-authoring tool to known people instead of relying on
/// IP allowlisting (dropped after the user's home IPs turned out to be dynamic). Registration is
/// self-service but requires a shared invite code (env var), so it's safe to expose publicly.
/// Session is a JWT in an HttpOnly cookie — the API validates it via the JwtBearer middleware
/// configured in Program.cs to read the cookie instead of an Authorization header.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    const string CookieName = "ueq_session";
    static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);

    readonly ContentDbContext _db;
    readonly IWebHostEnvironment _env;

    public AuthController(ContentDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Username and password are required.");

        var expectedCode = Encoding.UTF8.GetBytes(AuthConfig.InviteCode(_env));
        var givenCode = Encoding.UTF8.GetBytes(req.InviteCode ?? "");
        if (givenCode.Length != expectedCode.Length || !CryptographicOperations.FixedTimeEquals(givenCode, expectedCode))
            return Unauthorized("Invalid invite code.");

        var username = req.Username.Trim();
        if (await _db.WebAdmins.AnyAsync(a => a.Username == username))
            return Conflict("That username is already taken.");

        var admin = new WebAdmin
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(req.Password),
            CreatedAt = DateTime.UtcNow,
        };
        _db.WebAdmins.Add(admin);
        await _db.SaveChangesAsync();

        IssueSessionCookie(admin);
        return Ok(new AuthResponse(admin.Username));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var admin = await _db.WebAdmins.FirstOrDefaultAsync(a => a.Username == req.Username);
        if (admin == null || !PasswordHasher.Verify(req.Password, admin.PasswordHash))
            return Unauthorized("Invalid username or password.");

        IssueSessionCookie(admin);
        return Ok(new AuthResponse(admin.Username));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        return Ok();
    }

    [HttpGet("me")]
    public ActionResult<AuthResponse> Me()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (username == null) return Unauthorized();
        return Ok(new AuthResponse(username));
    }

    void IssueSessionCookie(WebAdmin admin)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Name, admin.Username),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthConfig.JwtSecret(_env)));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(SessionLifetime),
            signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append(CookieName, jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(SessionLifetime),
            Path = "/",
        });
    }
}
