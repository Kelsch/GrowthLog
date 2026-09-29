namespace GrowthLog.Web.Models;

public class FamilyConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string SourceFamilyId { get; set; } = string.Empty;
    public string TargetFamilyId { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
}

/// <summary>
/// A connection joined with the target family's name and the current
/// permission grants, for display in the sharing settings UI.
/// </summary>
public class FamilyConnectionView
{
    public string Id { get; set; } = string.Empty;
    public string SourceFamilyId { get; set; } = string.Empty;
    public string TargetFamilyId { get; set; } = string.Empty;
    public string TargetFamilyName { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsActive { get; set; }
    public bool ViewChildren { get; set; }
    public bool ViewAdults { get; set; }
    public bool ViewAll { get; set; }
}
