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
}
