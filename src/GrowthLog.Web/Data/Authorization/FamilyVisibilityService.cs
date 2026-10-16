using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Authorization;

/// <summary>
/// How the current user can see a family.
/// </summary>
public abstract record FamilyAccess
{
    /// <summary>The user is a member of the family: sees all active people.</summary>
    public sealed record Member : FamilyAccess;

    /// <summary>
    /// The user sees the family only via a sharing connection. The granted
    /// permission kinds determine which people are visible.
    /// </summary>
    public sealed record Shared(bool AllowAdults, bool AllowChildren) : FamilyAccess;
}

/// <summary>
/// The single source of truth for family and person visibility.
///
/// Every caller that lists families, people, or chart data MUST go through
/// this service. There is no second path.
///
/// Direction convention (matches FamilyConnection):
///   SourceFamilyId = the family that OWNS the data being shared out.
///   TargetFamilyId = the family that RECEIVES visibility.
///
/// Visibility rules:
///   (1) Membership: the user has a FamilyMembership in the family.
///       → sees all active people in that family.
///   (2) Sharing: the family is the Source of an active FamilyConnection
///       whose Target family the user belongs to, AND that connection has
///       at least one granted SharingPermission (IsGranted = 1).
///       → sees only people whose MembershipKind matches the granted kinds
///         (ViewAll grants both).
/// </summary>
public class FamilyVisibilityService
{
    private readonly IDbConnectionFactory _factory;

    public FamilyVisibilityService(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Returns the access map for the user: familyId → FamilyAccess.
    /// Membership wins over sharing when both apply.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, FamilyAccess>> GetAccessMapAsync(string userId)
    {
        using var connection = _factory.Create();

        // Step 1: membership families.
        var memberFamilyIds = (await connection.QueryAsync<string>("""
            SELECT fm.FamilyId
            FROM FamilyMemberships fm
            WHERE fm.UserId = @UserId
            """,
            new { UserId = userId })).ToList();

        // Step 2: shared sources with granted-kind flags.
        var sharedRows = (await connection.QueryAsync<SharedRow>("""
            SELECT
                c.SourceFamilyId,
                MAX(CASE WHEN sp.PermissionKind = 'ViewAll'      AND sp.IsGranted = 1 THEN 1 ELSE 0 END) AS ViewAll,
                MAX(CASE WHEN sp.PermissionKind = 'ViewAdults'   AND sp.IsGranted = 1 THEN 1 ELSE 0 END) AS ViewAdults,
                MAX(CASE WHEN sp.PermissionKind = 'ViewChildren' AND sp.IsGranted = 1 THEN 1 ELSE 0 END) AS ViewChildren
            FROM FamilyConnections c
            JOIN FamilyMemberships fm
                ON fm.FamilyId = c.TargetFamilyId
               AND fm.UserId = @UserId
            JOIN SharingPermissions sp
                ON sp.FamilyConnectionId = c.Id
            WHERE c.IsActive = 1
            GROUP BY c.SourceFamilyId
            HAVING ViewAll = 1 OR ViewAdults = 1 OR ViewChildren = 1
            """,
            new { UserId = userId })).ToList();

        // Step 3: build the access map. Membership wins over sharing.
        var map = new Dictionary<string, FamilyAccess>(StringComparer.Ordinal);

        foreach (var id in memberFamilyIds)
        {
            map[id] = new FamilyAccess.Member();
        }

        foreach (var row in sharedRows)
        {
            if (map.ContainsKey(row.SourceFamilyId)) continue;

            var allowAdults = row.ViewAll == 1 || row.ViewAdults == 1;
            var allowChildren = row.ViewAll == 1 || row.ViewChildren == 1;

            map[row.SourceFamilyId] = new FamilyAccess.Shared(allowAdults, allowChildren);
        }

        return map;
    }

    /// <summary>
    /// Family chips = access map keys. This is the ONLY method that lists
    /// families for the UI.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetVisibleFamilyIdsAsync(string userId)
    {
        var map = await GetAccessMapAsync(userId);
        return map.Keys.ToList();
    }

    /// <summary>
    /// Returns the IDs of people visible to the user, applying the kind
    /// filter for shared families.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetVisiblePersonIdsAsync(string userId)
    {
        var map = await GetAccessMapAsync(userId);
        if (map.Count == 0) return Array.Empty<string>();

        using var connection = _factory.Create();

        // Step 4: people in access-map families.
        var rows = (await connection.QueryAsync<PersonRow>("""
            SELECT p.Id, pfm.FamilyId, pfm.MembershipKind
            FROM People p
            JOIN PersonFamilyMembership pfm
                ON pfm.PersonId = p.Id
               AND pfm.IsActive = 1
            WHERE p.IsDeleted = 0
              AND pfm.FamilyId IN @FamilyIds
            """,
            new { FamilyIds = map.Keys.ToList() })).ToList();

        // Step 5: filter shared families by MembershipKind.
        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!map.TryGetValue(row.FamilyId, out var access)) continue;

            switch (access)
            {
                case FamilyAccess.Member:
                    visible.Add(row.Id);
                    break;
                case FamilyAccess.Shared shared:
                    if (row.MembershipKind == MembershipKinds.Adult && shared.AllowAdults)
                        visible.Add(row.Id);
                    else if (row.MembershipKind == MembershipKinds.Child && shared.AllowChildren)
                        visible.Add(row.Id);
                    break;
            }
        }

        return visible;
    }

    /// <summary>
    /// True if the user can see the given person.
    /// </summary>
    public async Task<bool> CanViewPersonAsync(string userId, string personId)
    {
        var visible = await GetVisiblePersonIdsAsync(userId);
        return visible.Contains(personId);
    }

    private sealed class SharedRow
    {
        public string SourceFamilyId { get; set; } = string.Empty;
        public int ViewAll { get; set; }
        public int ViewAdults { get; set; }
        public int ViewChildren { get; set; }
    }

    private sealed class PersonRow
    {
        public string Id { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public string MembershipKind { get; set; } = string.Empty;
    }
}
