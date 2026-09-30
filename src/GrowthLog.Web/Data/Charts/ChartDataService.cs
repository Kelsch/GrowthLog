using Dapper;
using GrowthLog.Web.Data.Authorization;

namespace GrowthLog.Web.Data.Charts;

/// <summary>
/// Builds chart series from measurements, enforcing the same authorization
/// rules as the rest of the app. Every query embeds VisiblePeopleSql so the
/// database only returns people the user is allowed to see.
/// </summary>
public class ChartDataService
{
    private readonly IDbConnectionFactory _factory;

    public ChartDataService(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Returns chart series for the given people (or all visible people when
    /// personIds is null/empty), optionally restricted to a set of families.
    /// Only actual measurements are returned — no interpolation.
    /// </summary>
    public async Task<IReadOnlyList<ChartSeries>> GetSeriesAsync(
        string userId,
        string metric,
        IReadOnlyCollection<string>? personIds = null,
        IReadOnlyCollection<string>? familyIds = null)
    {
        var valueColumn = metric == ChartMetrics.Weight ? "m.WeightKg" : "m.HeightCm";

        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<SeriesRow>($"""
            SELECT
                p.Id            AS PersonId,
                p.FirstName,
                p.MiddleName,
                p.LastName,
                p.DateOfBirth,
                pfm.FamilyId    AS FamilyId,
                f.Name          AS FamilyName,
                m.MeasurementDate,
                {valueColumn}   AS Value
            FROM Measurements m
            JOIN People p ON p.Id = m.PersonId AND p.IsDeleted = 0
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id AND pfm.IsActive = 1
            JOIN Families f ON f.Id = pfm.FamilyId AND f.IsDeleted = 0
            WHERE m.IsDeleted = 0
              AND {valueColumn} IS NOT NULL
              AND p.Id IN ({FamilyAuthorizationService.VisiblePeopleSql})
            ORDER BY p.FirstName, p.LastName, m.MeasurementDate
            """,
            new { UserId = userId });

        var personFilter = personIds is { Count: > 0 }
            ? new HashSet<string>(personIds, StringComparer.Ordinal)
            : null;
        var familyFilter = familyIds is { Count: > 0 }
            ? new HashSet<string>(familyIds, StringComparer.Ordinal)
            : null;

        var series = new Dictionary<string, ChartSeries>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (personFilter is not null && !personFilter.Contains(row.PersonId)) continue;
            if (familyFilter is not null && !familyFilter.Contains(row.FamilyId)) continue;

            if (!series.TryGetValue(row.PersonId, out var s))
            {
                s = new ChartSeries
                {
                    PersonId = row.PersonId,
                    PersonName = BuildName(row.FirstName, row.MiddleName, row.LastName),
                    FamilyId = row.FamilyId,
                    FamilyName = row.FamilyName
                };
                series[row.PersonId] = s;
            }

            if (!DateOnly.TryParse(row.DateOfBirth, out var dob)) continue;
            if (!DateOnly.TryParse(row.MeasurementDate, out var measured)) continue;

            var age = AgeCalculator.GetAgeYears(dob, measured);

            s.Points.Add(new ChartPoint
            {
                X = age,
                Y = row.Value,
                MeasurementDate = row.MeasurementDate,
                AgeYears = age
            });
        }

        return series.Values
            .OrderBy(s => s.FamilyName)
            .ThenBy(s => s.PersonName)
            .ToList();
    }

    private static string BuildName(string first, string? middle, string? last)
    {
        var parts = new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts);
    }

    private sealed class SeriesRow
    {
        public string PersonId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string DateOfBirth { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string MeasurementDate { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
