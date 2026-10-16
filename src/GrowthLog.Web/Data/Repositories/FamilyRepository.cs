using Dapper;
using GrowthLog.Web.Data.Authorization;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

/// <summary>
/// A family the user can see, plus whether that visibility comes from a
/// sharing connection rather than direct membership.
/// </summary>
public class VisibleFamilyRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool IsShared { get; set; }
}

public class FamilyRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly FamilyVisibilityService _visibility;

    public FamilyRepository(
        IDbConnectionFactory factory,
        FamilyVisibilityService visibility)
    {
        _factory = factory;
        _visibility = visibility;
    }

    public async Task<Family?> GetByIdAsync(string id)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<Family>(
            "SELECT * FROM Families WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id });
    }

    public async Task<IReadOnlyList<Family>> GetForUserAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Family>("""
            SELECT f.*
            FROM Families f
            JOIN FamilyMemberships fm ON fm.FamilyId = f.Id
            WHERE fm.UserId = @UserId AND f.IsDeleted = 0
            ORDER BY f.Name
            """,
            new { UserId = userId });
        return rows.ToList();
    }

    /// <summary>
    /// Returns every family the user can see. This is the SINGLE method that
    /// populates the family chips / family filter in the UI (Home embed and
    /// /compare both call it via CompareGrowth.razor).
    ///
    /// Visibility is delegated entirely to
    /// <see cref="FamilyVisibilityService.GetAccessMapAsync"/>. The access
    /// map keys are the visible family IDs; a family is "shared" when the
    /// user is not a member of it.
    ///
    /// Membership-only callers should keep using GetForUserAsync.
    /// </summary>
    public async Task<IReadOnlyList<VisibleFamilyRow>> GetVisibleForUserAsync(string userId)
    {
        var accessMap = await _visibility.GetAccessMapAsync(userId);
        if (accessMap.Count == 0) return Array.Empty<VisibleFamilyRow>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<VisibleFamilyRow>("""
            SELECT
                f.Id,
                f.Name,
                f.CreatedUtc,
                f.IsDeleted
            FROM Families f
            WHERE f.Id IN @FamilyIds
            ORDER BY f.Name
            """,
            new { FamilyIds = accessMap.Keys.ToList() });

        return rows
            .Select(r =>
            {
                r.IsShared = accessMap[r.Id] is FamilyAccess.Shared;
                return r;
            })
            .ToList();
    }

    public async Task<string> CreateAsync(string name, string ownerUserId)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        var family = new Family { Name = name };
        await connection.ExecuteAsync("""
            INSERT INTO Families (Id, Name, CreatedUtc, IsDeleted)
            VALUES (@Id, @Name, @CreatedUtc, 0)
            """, family, tx);

        await connection.ExecuteAsync("""
            INSERT INTO FamilyMemberships (Id, FamilyId, UserId, Role, JoinedUtc)
            VALUES (@Id, @FamilyId, @UserId, @Role, @JoinedUtc)
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                FamilyId = family.Id,
                UserId = ownerUserId,
                Role = FamilyRoles.Owner,
                JoinedUtc = DateTime.UtcNow.ToString("o")
            }, tx);

        tx.Commit();
        return family.Id;
    }

    public async Task RenameAsync(string familyId, string name)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "UPDATE Families SET Name = @Name WHERE Id = @Id",
            new { Id = familyId, Name = name });
    }

    public async Task SoftDeleteAsync(string familyId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "UPDATE Families SET IsDeleted = 1 WHERE Id = @Id",
            new { Id = familyId });
    }

    /// <summary>
    /// Sets (or clears, when <paramref name="familyId"/> is null) the user's
    /// starred/default family. Stored on AspNetUsers.DefaultFamilyId.
    /// </summary>
    public async Task SetDefaultFamilyAsync(string userId, string? familyId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "UPDATE AspNetUsers SET DefaultFamilyId = @FamilyId WHERE Id = @UserId",
            new { UserId = userId, FamilyId = familyId });
    }
}
