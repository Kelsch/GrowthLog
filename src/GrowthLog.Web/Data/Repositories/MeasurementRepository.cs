using Dapper;
using GrowthLog.Web.Data.Authorization;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class MeasurementRepository
{
    private readonly IDbConnectionFactory _factory;

    public MeasurementRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Returns measurements for every person the user is authorized to see.
    /// Authorization is enforced in SQL — no post-filtering.
    /// </summary>
    public async Task<IReadOnlyList<Measurement>> GetVisibleForUserAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>($"""
            SELECT m.*
            FROM Measurements m
            WHERE m.IsDeleted = 0
              AND m.PersonId IN ({FamilyAuthorizationService.VisiblePeopleSql})
            ORDER BY m.MeasurementDate DESC
            """,
            new { UserId = userId });
        return rows.ToList();
    }

    /// <summary>
    /// Returns measurements for a specific person, but only if the user is
    /// authorized to see that person. Returns an empty list otherwise.
    /// </summary>
    public async Task<IReadOnlyList<Measurement>> GetVisibleForUserForPersonAsync(string userId, string personId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Measurement>($"""
            SELECT m.*
            FROM Measurements m
            WHERE m.PersonId = @PersonId
              AND m.IsDeleted = 0
              AND m.PersonId IN ({FamilyAuthorizationService.VisiblePeopleSql})
            ORDER BY m.MeasurementDate DESC
            """,
            new { UserId = userId, PersonId = personId });
        return rows.ToList();
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
