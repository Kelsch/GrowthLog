using Dapper;
using GrowthLog.Web.Data.Authorization;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

/// <summary>
/// A person plus the family they belong to and whether the current user sees
/// them via a sharing connection rather than direct membership.
/// </summary>
public class VisiblePersonRow
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string DateOfBirth { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public string FamilyId { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public bool IsShared { get; set; }

    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(" ", parts);
        }
    }
}

/// <summary>
/// A person created by the current user, with a count of active family
/// memberships so the UI can highlight unassigned people.
/// </summary>
public class CreatedPersonRow
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string DateOfBirth { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public int ActiveFamilyCount { get; set; }

    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(" ", parts);
        }
    }
}

public class PersonRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly FamilyVisibilityService _visibility;

    public PersonRepository(
        IDbConnectionFactory factory,
        FamilyVisibilityService visibility)
    {
        _factory = factory;
        _visibility = visibility;
    }

    /// <summary>
    /// Returns all people the user is authorized to see, across every family
    /// they belong to plus any connected families that have granted sharing.
    /// Authorization is enforced in SQL — no post-filtering.
    /// </summary>
    public async Task<IReadOnlyList<Person>> GetVisibleForUserAsync(string userId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<Person>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>("""
            SELECT p.*
            FROM People p
            WHERE p.Id IN @Ids
            ORDER BY p.FirstName, p.LastName
            """,
            new { Ids = visibleIds.ToList() });
        return rows.ToList();
    }

    /// <summary>
    /// Returns people visible to the user who are members of the given family.
    /// </summary>
    public async Task<IReadOnlyList<Person>> GetVisibleForUserInFamilyAsync(string userId, string familyId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<Person>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>("""
            SELECT p.*
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id
            WHERE pfm.FamilyId = @FamilyId
              AND pfm.IsActive = 1
              AND p.Id IN @Ids
            ORDER BY p.FirstName, p.LastName
            """,
            new { Ids = visibleIds.ToList(), FamilyId = familyId });
        return rows.ToList();
    }

    /// <summary>
    /// Returns every person the user can see, together with the family they
    /// belong to and whether that visibility comes from a sharing connection
    /// (i.e. the user is not a member of that family).
    ///
    /// This is the query the People list, Home "Shared with me", and Compare
    /// all use. Visibility is enforced entirely by
    /// <see cref="FamilyAuthorizationService.VisiblePeopleSql"/>, which
    /// applies the ViewChildren / ViewAdults / ViewAll kind filter for shared
    /// families. A person in a shared family is returned only if the granted
    /// permission kind matches their MembershipKind (Child / Adult), or if
    /// ViewAll is granted.
    /// </summary>
    public async Task<IReadOnlyList<VisiblePersonRow>> GetVisiblePeopleWithFamilyAsync(string userId)
    {
        var accessMap = await _visibility.GetAccessMapAsync(userId);
        if (accessMap.Count == 0) return Array.Empty<VisiblePersonRow>();

        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<VisiblePersonRow>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<VisiblePersonRow>("""
            SELECT
                p.Id,
                p.UserId,
                p.FirstName,
                p.MiddleName,
                p.LastName,
                p.DateOfBirth,
                p.AvatarUrl,
                p.CreatedUtc,
                p.IsDeleted,
                pfm.FamilyId,
                f.Name AS FamilyName
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id AND pfm.IsActive = 1
            JOIN Families f ON f.Id = pfm.FamilyId AND f.IsDeleted = 0
            WHERE p.IsDeleted = 0
              AND p.Id IN @Ids
              AND pfm.FamilyId IN @FamilyIds
            ORDER BY f.Name, p.FirstName, p.LastName
            """,
            new
            {
                Ids = visibleIds.ToList(),
                FamilyIds = accessMap.Keys.ToList()
            });

        return rows
            .Select(r =>
            {
                r.IsShared = accessMap.TryGetValue(r.FamilyId, out var a)
                             && a is FamilyAccess.Shared;
                return r;
            })
            .ToList();
    }

    /// <summary>
    /// Returns people from *connected source families* that are shared into
    /// the given family (i.e. the given family is the Target of a
    /// FamilyConnection with a granted SharingPermission). Used by the family
    /// detail page.
    ///
    /// Share grants apply to target-family Owners only (not all target
    /// members). The join on FamilyMemberships therefore requires
    /// Role = 'Owner' — a Viewer/Child/Adult member of the target family
    /// does not see shared people through this path.
    ///
    /// The kind filter (ViewChildren / ViewAdults / ViewAll) is applied by
    /// <see cref="FamilyVisibilityService.GetVisiblePersonIdsAsync"/>; the
    /// extra predicate here only narrows to the specific target family. This
    /// keeps the family detail page consistent with Home and Compare.
    /// </summary>
    public async Task<IReadOnlyList<VisiblePersonRow>> GetSharedIntoFamilyAsync(string userId, string targetFamilyId)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<VisiblePersonRow>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<VisiblePersonRow>("""
            SELECT DISTINCT
                p.Id,
                p.UserId,
                p.FirstName,
                p.MiddleName,
                p.LastName,
                p.DateOfBirth,
                p.AvatarUrl,
                p.CreatedUtc,
                p.IsDeleted,
                pfm.FamilyId,
                f.Name AS FamilyName,
                1 AS IsShared
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id AND pfm.IsActive = 1
            JOIN Families f ON f.Id = pfm.FamilyId AND f.IsDeleted = 0
            JOIN FamilyConnections c
                ON c.SourceFamilyId = pfm.FamilyId
               AND c.TargetFamilyId = @TargetFamilyId
               AND c.IsActive = 1
            JOIN FamilyMemberships fm
                ON fm.FamilyId = c.TargetFamilyId
               AND fm.UserId = @UserId
               AND fm.Role = 'Owner'
            WHERE p.IsDeleted = 0
              AND p.Id IN @Ids
            ORDER BY f.Name, p.FirstName, p.LastName
            """,
            new { Ids = visibleIds.ToList(), UserId = userId, TargetFamilyId = targetFamilyId });
        return rows.ToList();
    }

    public async Task<Person?> GetByIdAsync(string id)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<Person>(
            "SELECT * FROM People WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id });
    }

    /// <summary>
    /// Searches people the user is authorized to see by name (first/last/middle).
    /// Authorization is enforced in SQL via VisiblePeopleSql — the user can
    /// never discover people they are not allowed to see.
    /// </summary>
    public async Task<IReadOnlyList<Person>> SearchVisibleAsync(string userId, string? query, int limit = 25)
    {
        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<Person>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>("""
            SELECT p.*
            FROM People p
            WHERE p.IsDeleted = 0
              AND p.Id IN @Ids
              AND (
                    @Query IS NULL OR @Query = ''
                 OR p.FirstName LIKE @Like
                 OR p.LastName  LIKE @Like
                 OR p.MiddleName LIKE @Like
                 OR (p.FirstName || ' ' || COALESCE(p.LastName, '')) LIKE @Like
              )
            ORDER BY p.FirstName, p.LastName
            LIMIT @Limit
            """,
            new
            {
                Ids = visibleIds.ToList(),
                Query = query,
                Like = $"%{query}%",
                Limit = limit
            });
        return rows.ToList();
    }

    /// <summary>
    /// Returns the given people, but only those the user is authorized to see.
    /// Used to validate that a person being linked to a family is visible to
    /// the acting user.
    /// </summary>
    public async Task<IReadOnlyList<Person>> GetVisibleByIdsAsync(string userId, IEnumerable<string> personIds)
    {
        var ids = personIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return Array.Empty<Person>();

        var visibleIds = await _visibility.GetVisiblePersonIdsAsync(userId);
        if (visibleIds.Count == 0) return Array.Empty<Person>();

        var allowed = ids.Where(visibleIds.Contains).ToList();
        if (allowed.Count == 0) return Array.Empty<Person>();

        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>("""
            SELECT p.*
            FROM People p
            WHERE p.IsDeleted = 0
              AND p.Id IN @Ids
            """,
            new { Ids = allowed });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Person>> GetForFamilyAsync(string familyId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>("""
            SELECT p.*
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id
            WHERE pfm.FamilyId = @FamilyId
              AND pfm.IsActive = 1
              AND p.IsDeleted = 0
            ORDER BY p.FirstName, p.LastName
            """,
            new { FamilyId = familyId });
        return rows.ToList();
    }

    public async Task<string> CreateAsync(Person person)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO People (Id, UserId, FirstName, MiddleName, LastName, DateOfBirth, AvatarUrl, CreatedUtc, CreatedByUserId, IsDeleted)
            VALUES (@Id, @UserId, @FirstName, @MiddleName, @LastName, @DateOfBirth, @AvatarUrl, @CreatedUtc, @CreatedByUserId, 0)
            """, person);
        return person.Id;
    }

    /// <summary>
    /// Returns people created by the given user, together with the number of
    /// active family memberships each has. Used by the "People I created" page
    /// to surface unassigned people.
    /// </summary>
    public async Task<IReadOnlyList<CreatedPersonRow>> GetCreatedByUserAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<CreatedPersonRow>("""
            SELECT
                p.Id,
                p.UserId,
                p.FirstName,
                p.MiddleName,
                p.LastName,
                p.DateOfBirth,
                p.AvatarUrl,
                p.CreatedUtc,
                p.CreatedByUserId,
                p.IsDeleted,
                (SELECT COUNT(1)
                 FROM PersonFamilyMembership pfm
                 WHERE pfm.PersonId = p.Id AND pfm.IsActive = 1) AS ActiveFamilyCount
            FROM People p
            WHERE p.IsDeleted = 0
              AND p.CreatedByUserId = @UserId
            ORDER BY p.FirstName, p.LastName
            """,
            new { UserId = userId });
        return rows.ToList();
    }

    public async Task UpdateAsync(Person person)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE People
            SET FirstName = @FirstName,
                MiddleName = @MiddleName,
                LastName = @LastName,
                DateOfBirth = @DateOfBirth,
                AvatarUrl = @AvatarUrl,
                UserId = @UserId
            WHERE Id = @Id
            """, person);
    }

    public async Task SoftDeleteAsync(string personId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "UPDATE People SET IsDeleted = 1 WHERE Id = @Id",
            new { Id = personId });
    }
}
