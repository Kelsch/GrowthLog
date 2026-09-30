namespace GrowthLog.Web.Data.Charts;

/// <summary>
/// A single plotted point. X is either an age in decimal years or a date
/// (ISO-8601), depending on the chart's X-axis mode. Y is the metric value
/// in canonical units (cm or kg).
/// </summary>
public class ChartPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public string MeasurementDate { get; set; } = string.Empty;
    public double AgeYears { get; set; }
}

/// <summary>
/// One person's series on a chart. Points are always actual measurements —
/// never interpolated or fabricated.
/// </summary>
public class ChartSeries
{
    public string PersonId { get; set; } = string.Empty;
    public string PersonName { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public List<ChartPoint> Points { get; set; } = new();
}

public static class ChartMetrics
{
    public const string Height = "Height";
    public const string Weight = "Weight";

    public static readonly string[] All = { Height, Weight };
}

public static class ChartXAxisModes
{
    public const string Age = "Age";
    public const string Date = "Date";

    public static readonly string[] All = { Age, Date };
}
