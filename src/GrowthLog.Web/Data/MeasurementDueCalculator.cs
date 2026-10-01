namespace GrowthLog.Web.Data;

public static class MeasurementDueStatuses
{
    public const string Completed = "Completed";
    public const string Upcoming = "Upcoming";
    public const string DueSoon = "DueSoon";
    public const string DueToday = "DueToday";
    public const string Overdue = "Overdue";
    public const string NeverMeasured = "NeverMeasured";
}

/// <summary>
/// Result of evaluating whether a person is due for their yearly birthday
/// measurement.
/// </summary>
public class MeasurementDue
{
    public string Status { get; set; } = MeasurementDueStatuses.NeverMeasured;
    public DateOnly NextBirthday { get; set; }
    public int DaysUntilDue { get; set; }
    public string? LastMeasurementDate { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>
/// Computes the yearly birthday measurement due state from a date of birth,
/// the last measurement date, and today's date. Pure logic — no data access.
/// </summary>
public static class MeasurementDueCalculator
{
    /// <summary>Days before the birthday that count as "due soon".</summary>
    public const int DueSoonWindowDays = 14;

    public static MeasurementDue Evaluate(
        DateOnly dateOfBirth,
        string? lastMeasurementDate,
        DateOnly today)
    {
        var next = NextBirthday(dateOfBirth, today);
        var days = next.DayNumber - today.DayNumber;

        DateOnly? last = null;
        if (!string.IsNullOrWhiteSpace(lastMeasurementDate)
            && DateOnly.TryParse(lastMeasurementDate, out var parsed))
        {
            last = parsed;
        }

        var result = new MeasurementDue
        {
            NextBirthday = next,
            DaysUntilDue = days,
            LastMeasurementDate = last?.ToString("yyyy-MM-dd")
        };

        if (last is null)
        {
            result.Status = MeasurementDueStatuses.NeverMeasured;
            result.StatusText = "No measurements yet";
            return result;
        }

        // Already measured since the most recent birthday?
        var mostRecentBirthday = MostRecentBirthday(dateOfBirth, today);
        if (last.Value >= mostRecentBirthday)
        {
            result.Status = MeasurementDueStatuses.Completed;
            result.StatusText = "Measured this year";
            return result;
        }

        if (days < 0)
        {
            result.Status = MeasurementDueStatuses.Overdue;
            result.StatusText = $"{Math.Abs(days)} day{(Math.Abs(days) == 1 ? "" : "s")} overdue";
        }
        else if (days == 0)
        {
            result.Status = MeasurementDueStatuses.DueToday;
            result.StatusText = "Due today";
        }
        else if (days <= DueSoonWindowDays)
        {
            result.Status = MeasurementDueStatuses.DueSoon;
            result.StatusText = days == 1 ? "Due tomorrow" : $"Due in {days} days";
        }
        else
        {
            result.Status = MeasurementDueStatuses.Upcoming;
            result.StatusText = $"Due in {days} days";
        }

        return result;
    }

    public static DateOnly NextBirthday(DateOnly dob, DateOnly today)
    {
        var candidate = new DateOnly(today.Year, dob.Month, dob.Day);
        if (candidate < today) candidate = new DateOnly(today.Year + 1, dob.Month, dob.Day);
        return candidate;
    }

    public static DateOnly MostRecentBirthday(DateOnly dob, DateOnly today)
    {
        var candidate = new DateOnly(today.Year, dob.Month, dob.Day);
        if (candidate > today) candidate = new DateOnly(today.Year - 1, dob.Month, dob.Day);
        return candidate;
    }

    /// <summary>
    /// Sort key: overdue/due first, then soonest upcoming, then completed,
    /// then never measured.
    /// </summary>
    public static int SortKey(MeasurementDue due) => due.Status switch
    {
        MeasurementDueStatuses.Overdue => 0,
        MeasurementDueStatuses.DueToday => 1,
        MeasurementDueStatuses.DueSoon => 2,
        MeasurementDueStatuses.Upcoming => 3,
        MeasurementDueStatuses.Completed => 4,
        _ => 5
    };
}
