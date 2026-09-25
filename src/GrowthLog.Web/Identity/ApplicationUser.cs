using Microsoft.AspNetCore.Identity;

namespace GrowthLog.Web.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"Imperial" or "Metric".</summary>
    public string PreferredUnitSystem { get; set; } = "Imperial";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
