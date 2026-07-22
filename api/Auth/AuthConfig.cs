namespace Ueq.ContentApi.Auth;

/// <summary>
/// Resolves the two secrets the web-admin auth flow needs (5.11). Mirrors the resolution style
/// already used for the DB connection string (env var, no config file) — <c>UEQ_WEB_JWT_SECRET</c>
/// signs/validates session tokens, <c>UEQ_WEB_INVITE_CODE</c> gates self-service registration so
/// it's safe to expose publicly. Both require a real env var in Production; a fixed insecure
/// default is used in Development only, purely so `dotnet run` works out of the box locally.
/// </summary>
public static class AuthConfig
{
    public static string JwtSecret(IWebHostEnvironment env) =>
        Environment.GetEnvironmentVariable("UEQ_WEB_JWT_SECRET")
        ?? (env.IsDevelopment()
            ? "dev-only-jwt-secret-do-not-use-in-production-0123456789abcdef"
            : throw new InvalidOperationException("UEQ_WEB_JWT_SECRET must be set in Production."));

    public static string InviteCode(IWebHostEnvironment env) =>
        Environment.GetEnvironmentVariable("UEQ_WEB_INVITE_CODE")
        ?? (env.IsDevelopment()
            ? "dev-invite"
            : throw new InvalidOperationException("UEQ_WEB_INVITE_CODE must be set in Production."));
}
