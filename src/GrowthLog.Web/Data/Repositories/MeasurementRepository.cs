using Dapper;
using GrowthLog.Web.Data.Authorization;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

/// <summary>
/// Authorization-scoped aggregate counts for the Dashboard summary strip.
/// </summary>
public class MeasurementSummary
{
    public int MeasurementCount { get; set; }
    public string? EarliestDate { get; set; }
    public string? LatestDate { get; set; }
}

public class MeasurementRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly FamilyVisibilityService _visibility;

    public MeasurementRepository(
        IDbConnectionFactory factory,
        FamilyVisibilityService visibility)
    {
        _factory = factory;
        _visibility = visibility;
    }

    /// <summary>
    /// Returns measurements for every person the user is authorized to see.
    /// Authorization is enforced in SQL — no post-filtering.
    /// </summary>
    public async Task<IReadOnlyList<Measurement>> GetVisibleForUserAsync(string userId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<Measurement>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>("""
            SELECT m.*
            FROM Measurements m
            WHERE m.IsDeleted = 0
              AND m.PersonId IN @Ids
            ORDER BY m.MeasurementDate DESC
            """,
            new { Ids = visibleIds.ToList() });
        return rows.ToList();
    }

    /// <summary>
    /// Returns the most recent measurements the user is authorized to see,
    /// newest first, capped in SQL. This is the single source of truth for
    /// Dashboard "Recent activity" — callers must not fetch a global list and
    /// filter in the UI.
    ///
    /// Authorization is enforced in SQL via
    /// <see cref="FamilyAuthorizationService.VisiblePeopleSql"/>, which
    /// applies the ViewChildren / ViewAdults / ViewAll kind filter for shared
    /// families. Measurements for a person in a shared family are returned
    /// only if the granted permission kind matches that person's
    /// MembershipKind (or ViewAll is granted).
    /// </summary>
    public async Task<IReadOnlyList<Measurement>> GetRecentVisibleForUserAsync(string userId, int limit = 10)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<Measurement>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>("""
            SELECT m.*
            FROM Measurements m
            WHERE m.IsDeleted = 0
              AND m.PersonId IN @Ids
            ORDER BY m.MeasurementDate DESC
            LIMIT @Limit
            """,
            new { Ids = visibleIds.ToList(), Limit = limit });
        return rows.ToList();
    }

    /// <summary>
    /// Returns authorization-scoped aggregate counts for the Dashboard summary
    /// strip: number of measurements and the earliest/latest measurement dates
    /// across people the user is allowed to see. Enforced in SQL so summary
    /// counts can never include other families' data.
    /// </summary>
    public async Task<MeasurementSummary> GetVisibleSummaryAsync(string userId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return new MeasurementSummary();

        using var connection = _factory.Create();
        var row = await connection.QuerySingleAsync<MeasurementSummary>("""
            SELECT
                COUNT(1) AS MeasurementCount,
                MIN(m.MeasurementDate) AS EarliestDate,
                MAX(m.MeasurementDate) AS LatestDate
            FROM Measurements m
            WHERE m.IsDeleted = 0
              AND m.PersonId IN @Ids
            """,
            new { Ids = visibleIds.ToList() });
        return row;
    }

    /// <summary>
    /// Returns measurements for a specific person, but only if the user is
    /// authorized to see that person. Returns an empty list otherwise.
    /// </summary>
    public async Task<IReadOnlyList<Measurement>> GetVisibleForUserForPersonAsync(string userId, string personId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (!visibleIds.Contains(personId)) return Array.Empty<Measurement>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>("""
            SELECT m.*
            FROM Measurements m
            WHERE m.PersonId = @PersonId
              AND m.IsDeleted = 0
            ORDER BY m.MeasurementDate DESC
            """,
            new { PersonId = personId });
        return rows.ToList();
    }

    /// <summary>
    /// Returns the latest measurement date per person across every person the
    /// user is authorized to see. Used by the Dashboard "upcoming measurements"
    /// calculation so it never has to load the full measurement history.
    /// Authorization is enforced in SQL via VisiblePeopleSql.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetLatestDatePerVisiblePersonAsync(string userId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<(string PersonId, string MaxDate)>("""
            SELECT m.PersonId, MAX(m.MeasurementDate) AS MaxDate
            FROM Measurements m
            WHERE m.IsDeleted = 0
              AND m.PersonId IN @Ids
            GROUP BY m.PersonId
            """,
            new { Ids = visibleIds.ToList() });

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            result[row.PersonId] = row.MaxDate;
        }
        return result;
    }

    /// <summary>
    /// Returns the latest measurement per person for the given people, but
    /// only for people the user is authorized to see. Used by family member
    /// rows to show latest height/weight without N+1 queries.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, Measurement>> GetLatestForPeopleAsync(
        string userId,
        IEnumerable<string> personIds)
    {
        var ids = personIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return new Dictionary<string, Measurement>(StringComparer.Ordinal);

        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return new Dictionary<string, Measurement>(StringComparer.Ordinal);

        var allowed = ids.Where(visibleIds.Contains).ToList();
        if (allowed.Count == 0) return new Dictionary<string, Measurement>(StringComparer.Ordinal);

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>("""
            SELECT m.*
            FROM Measurements m
            JOIN (
                SELECT PersonId, MAX(MeasurementDate) AS MaxDate
                FROM Measurements
                WHERE IsDeleted = 0
                  AND PersonId IN @Ids
                GROUP BY PersonId
            ) latest
              ON latest.PersonId = m.PersonId
             AND latest.MaxDate = m.MeasurementDate
            WHERE m.IsDeleted = 0
            """,
            new { Ids = allowed });

        var result = new Dictionary<string, Measurement>(StringComparer.Ordinal);
        foreach (var m in rows)
        {
            // If multiple measurements share the max date, keep the first.
            if (!result.ContainsKey(m.PersonId))
            {
                result[m.PersonId] = m;
            }
        }
        return result;
    }

    public async Task<Measurement?> GetByIdAsync(string id)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<Measurement>(
            "SELECT * FROM Measurements WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id });
    }

    public async Task<IReadOnlyList<Measurement>> GetForPersonAsync(string personId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>("""
            SELECT * FROM Measurements
            WHERE PersonId = @PersonId AND IsDeleted = 0
            ORDER BY MeasurementDate DESC
            """,
            new { PersonId = personId });
        return rows.ToList();
    }

    public async Task<string> CreateAsync(Measurement measurement)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO Measurements
                (Id, PersonId, MeasurementDate, HeightCm, WeightKg, Notes,
                 EnteredByUserId, CreatedUtc, ModifiedUtc, IsDeleted)
            VALUES
                (@Id, @PersonId, @MeasurementDate, @HeightCm, @WeightKg, @Notes,
                 @EnteredByUserId, @CreatedUtc, NULL, 0)
            """, measurement);
        return measurement.Id;
    }

    public async Task UpdateAsync(Measurement measurement)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE Measurements
            SET MeasurementDate = @MeasurementDate,
                HeightCm = @HeightCm,
                WeightKg = @WeightKg,
                Notes = @Notes,
                ModifiedUtc = @ModifiedUtc
            WHERE Id = @Id AND IsDeleted = 0
            """, measurement);
    }

    public async Task SoftDeleteAsync(string measurementId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE Measurements
            SET IsDeleted = 1, ModifiedUtc = @ModifiedUtc
            WHERE Id = @Id
            """,
            new { Id = measurementId, ModifiedUtc = DateTime.UtcNow.ToString("o") });
    }
}
