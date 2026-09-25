namespace GrowthLog.Web.Data;

/// <summary>
/// Derives age from a date of birth and a measurement date.
/// Age is never stored; it is always computed.
/// </summary>
public static class AgeCalculator
{
    /// <summary>
    /// Age in decimal years, e.g. 5.25 for 5 years 3 months.
    /// Uses actual day counts so leap years and birthday boundaries are exact.
    /// </summary>
    public static double GetAgeYears(DateOnly dateOfBirth, DateOnly asOf)
    {
        if (asOf < dateOfBirth)
        {
            return 0;
        }

        var wholeYears = asOf.Year - dateOfBirth.Year;
        var anniversary = dateOfBirth.AddYears(wholeYears);
        if (anniversary > asOf)
        {
            wholeYears--;
            anniversary = dateOfBirth.AddYears(wholeYears);
        }

        var nextAnniversary = dateOfBirth.AddYears(wholeYears + 1);
        var daysIntoYear = asOf.DayNumber - anniversary.DayNumber;
        var daysInYear = nextAnniversary.DayNumber - anniversary.DayNumber;

        return wholeYears + (daysInYear == 0 ? 0 : (double)daysIntoYear / daysInYear);
    }

    /// <summary>
    /// Friendly age, e.g. "5 years, 3 months".
    /// </summary>
    public static string FormatAge(DateOnly dateOfBirth, DateOnly asOf)
    {
        if (asOf < dateOfBirth)
        {
            return "—";
        }

        var years = asOf.Year - dateOfBirth.Year;
        var months = asOf.Month - dateOfBirth.Month;
        var days = asOf.Day - dateOfBirth.Day;

        if (days < 0)
        {
            months--;
            var prevMonth = asOf.AddMonths(-1);
            days += DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
        }

        if (months < 0)
        {
            years--;
            months += 12;
        }

        var yearPart = years == 1 ? "1 year" : $"{years} years";
        var monthPart = months == 1 ? "1 month" : $"{months} months";

        if (years == 0 && months == 0)
        {
            return days == 1 ? "1 day" : $"{days} days";
        }

        if (years == 0)
        {
            return monthPart;
        }

        return months == 0 ? yearPart : $"{yearPart}, {monthPart}";
    }
}
