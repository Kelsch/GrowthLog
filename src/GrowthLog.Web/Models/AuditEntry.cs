namespace GrowthLog.Web.Models;

public class AuditEntry
{
    public long Id { get; set; }
    public string OccurredUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
}
