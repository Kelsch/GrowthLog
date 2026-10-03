using Microsoft.AspNetCore.Identity;

namespace GrowthLog.Web.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"Imperial" or "Metric".</summary>
    public string PreferredUnitSystem { get; set; } = "Imperial";

    /// <summary>Optional starred/default family shown first on the Dashboard.</summary>
    public string? DefaultFamilyId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
