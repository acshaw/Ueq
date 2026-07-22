namespace Ueq.ContentApi.Models;

/// <summary>Row in <c>web_admins</c> (5.11) — a person allowed to use the content-authoring tool.</summary>
public class WebAdmin
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
