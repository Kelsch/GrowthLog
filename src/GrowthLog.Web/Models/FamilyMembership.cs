namespace GrowthLog.Web.Models;

public static class FamilyRoles
{
    public const string Owner = "Owner";
    public const string Adult = "Adult";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { Owner, Adult, Viewer };

    public static bool CanMutate(string role) => role is Owner or Adult;
    public static bool CanAdminister(string role) => role == Owner;
}

public class FamilyMembership
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string FamilyId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = FamilyRoles.Viewer;
    public string JoinedUtc { get; set; } = DateTime.UtcNow.ToString("o");
}
