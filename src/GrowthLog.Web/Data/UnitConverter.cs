namespace GrowthLog.Web.Data;

/// <summary>
/// Converts between canonical metric storage and display units.
/// Storage is always cm / kg.
/// </summary>
public static class UnitConverter
{
    private const double CmPerInch = 2.54;
    private const double KgPerPound = 0.45359237;

    public static double InchesToCm(double inches) => inches * CmPerInch;
    public static double CmToInches(double cm) => cm / CmPerInch;
    public static double PoundsToKg(double pounds) => pounds * KgPerPound;
    public static double KgToPounds(double kg) => kg / KgPerPound;

    public static double FeetInchesToCm(int feet, double inches)
        => InchesToCm(feet * 12 + inches);

    public static (int Feet, double Inches) CmToFeetInches(double cm)
    {
        var totalInches = CmToInches(cm);
        var feet = (int)(totalInches / 12);
        var inches = totalInches - feet * 12;
        return (feet, inches);
    }

    public static string FormatHeight(double? cm, string unitSystem)
    {
        if (cm is null)
        {
            return "—";
        }

        if (unitSystem == "Metric")
        {
            return $"{cm.Value:0.0} cm";
        }

        var (feet, inches) = CmToFeetInches(cm.Value);
        return $"{feet}' {inches:0.0}\"";
    }

    public static string FormatWeight(double? kg, string unitSystem)
    {
        if (kg is null)
        {
            return "—";
        }

        return unitSystem == "Metric"
            ? $"{kg.Value:0.0} kg"
            : $"{KgToPounds(kg.Value):0.0} lb";
    }

    /// <summary>
    /// Normalizes a stored preference to either "Metric" or "Imperial".
    /// Anything unrecognized falls back to "Imperial" to match Settings.
    /// </summary>
    public static string NormalizeUnitSystem(string? unitSystem)
        => string.Equals(unitSystem, "Metric", StringComparison.OrdinalIgnoreCase)
            ? "Metric"
            : "Imperial";

    /// <summary>
    /// Converts a height entered in the user's preferred units into canonical cm.
    /// Returns null when no value was entered.
    /// </summary>
    public static double? ParseHeightToCm(string unitSystem, double? cm, int? feet, double? inches)
    {
        if (NormalizeUnitSystem(unitSystem) == "Metric")
        {
            return cm;
        }

        if (feet is null && inches is null)
        {
            return null;
        }

        return FeetInchesToCm(feet ?? 0, inches ?? 0);
    }

    /// <summary>
    /// Converts a weight entered in the user's preferred units into canonical kg.
    /// Returns null when no value was entered.
    /// </summary>
    public static double? ParseWeightToKg(string unitSystem, double? kg, double? pounds)
    {
        if (NormalizeUnitSystem(unitSystem) == "Metric")
        {
            return kg;
        }

        return pounds is null ? null : PoundsToKg(pounds.Value);
    }
}
