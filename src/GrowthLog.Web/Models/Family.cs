namespace GrowthLog.Web.Models;

public class Family
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string Name { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public bool IsDeleted { get; set; }
}
