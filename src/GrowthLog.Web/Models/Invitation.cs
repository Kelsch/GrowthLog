namespace GrowthLog.Web.Models;

public static class InvitationStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
}

public class Invitation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string Token { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string? InvitedEmail { get; set; }
    public string IntendedRole { get; set; } = FamilyRoles.Adult;
    public string InvitedByUserId { get; set; } = string.Empty;
    public string Status { get; set; } = InvitationStatuses.Pending;
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public string ExpiresUtc { get; set; } = DateTime.UtcNow.AddDays(14).ToString("o");
    public string? AcceptedUtc { get; set; }
    public string? AcceptedByUserId { get; set; }
}

/// <summary>Invitation joined with the family name for display.</summary>
public class InvitationView
{
    public string Id { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string? InvitedEmail { get; set; }
    public string IntendedRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string ExpiresUtc { get; set; } = string.Empty;
}
