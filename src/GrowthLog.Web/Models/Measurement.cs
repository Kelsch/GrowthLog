namespace GrowthLog.Web.Models;

public class Measurement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string PersonId { get; set; } = string.Empty;
    public string MeasurementDate { get; set; } = string.Empty; // YYYY-MM-DD
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public string? Notes { get; set; }
    public string EnteredByUserId { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public string? ModifiedUtc { get; set; }
    public bool IsDeleted { get; set; }
}
