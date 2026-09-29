namespace GrowthLog.Web.Models;

public static class SharingPermissionKinds
{
    public const string ViewChildren = "ViewChildren";
    public const string ViewAdults = "ViewAdults";
    public const string ViewAll = "ViewAll";

    public static readonly string[] All = { ViewChildren, ViewAdults, ViewAll };
}

public class SharingPermission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string FamilyConnectionId { get; set; } = string.Empty;
    public string PermissionKind { get; set; } = SharingPermissionKinds.ViewChildren;
    public bool IsGranted { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
}
