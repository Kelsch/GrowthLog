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

    public FamilyRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
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
    /// Returns every family the user can see: families they belong to, PLUS
    /// families shared into them via an active FamilyConnection whose source
    /// family has granted a SharingPermission that yields at least one
    /// visible person.
    ///
    /// The shared-family predicate is delegated to
    /// <see cref="FamilyAuthorizationService.SharedFamiliesSql"/>, which
    /// applies the same ViewChildren / ViewAdults / ViewAll kind filter as
    /// <see cref="FamilyAuthorizationService.VisiblePeopleSql"/>. This is the
    /// single authorization path used by both Home and Compare, so a family
    /// with only ViewChildren granted appears here only if it actually has a
    /// Child member, and a family with no granted permission does not appear
    /// at all.
    ///
    /// Membership-only callers should keep using GetForUserAsync.
    /// </summary>
    public async Task<IReadOnlyList<VisibleFamilyRow>> GetVisibleForUserAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<VisibleFamilyRow>($"""
            SELECT DISTINCT
                f.Id,
                f.Name,
                f.CreatedUtc,
                f.IsDeleted,
                CASE WHEN fm.UserId IS NULL THEN 1 ELSE 0 END AS IsShared
            FROM Families f
            LEFT JOIN FamilyMemberships fm
                ON fm.FamilyId = f.Id
               AND fm.UserId = @UserId
            WHERE f.IsDeleted = 0
              AND (
                    -- Rule 1: the user is a member of this family.
                    fm.UserId IS NOT NULL
                    -- Rule 4: the family is shared into the user via an active
                    -- connection with a granted permission that yields at
                    -- least one visible person (kind-aware).
                 OR f.Id IN ({FamilyAuthorizationService.SharedFamiliesSql})
              )
            ORDER BY f.Name
            """,
            new { UserId = userId });
        return rows.ToList();
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
