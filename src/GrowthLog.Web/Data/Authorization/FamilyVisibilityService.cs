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
///       whose Target family the user is an OWNER of, AND that connection
///       has at least one granted SharingPermission (IsGranted = 1).
///       → sees only people whose MembershipKind matches the granted kinds
///         (ViewAll grants both).
///
/// Share grants apply to target-family Owners only (not all target members).
/// A user who is merely a Viewer/Child/Adult member of the Target family
/// does NOT receive the share. This prevents a share into "Parents' family"
/// from leaking to every sibling who happens to be a member of that family.
///
/// Sharing is non-transitive. The recipient side of a share is always a
/// direct FamilyMembership in the connection's TargetFamilyId — never
/// "a family the user can see by any means". We do NOT walk chains of
/// FamilyConnections. Concretely:
///
///   * If P shares into T and the user is an Owner of T, the user sees P.
///   * If M shares into P, the user does NOT see M just because they see P.
///     Seeing P via sharing does not make the user an Owner of P, so the
///     M→P connection's TargetFamilyId (P) is not a family the user owns,
///     and M is correctly excluded.
///
/// Membership is likewise direct: being a member of F sees F, and nothing
/// else. There is no closure over either relation.
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
    ///
    /// Share grants apply to target-family Owners only (not all target
    /// members). The recipient side of a share is a direct FamilyMembership
    /// in the connection's TargetFamilyId with Role = 'Owner'. We never
    /// treat "families the user can see by any means" as the recipient set,
    /// and we never walk chains of FamilyConnections.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, FamilyAccess>> GetAccessMapAsync(string userId)
    {
        using var connection = _factory.Create();

        // Step 1: membership families. Direct membership only — no closure.
        // Soft-deleted families are excluded here so they can never appear as
        // a chip, and so their people/measurements are excluded downstream
        // (GetVisiblePersonIdsAsync derives its family set from this map).
        var memberFamilyIds = (await connection.QueryAsync<string>("""
            SELECT fm.FamilyId
            FROM FamilyMemberships fm
            JOIN Families f
                ON f.Id = fm.FamilyId
               AND f.IsDeleted = 0
            WHERE fm.UserId = @UserId
            """,
            new { UserId = userId })).ToList();

        // Step 2: shared sources with granted-kind flags.
        //
        // Share grants apply to target-family Owners only (not all target
        // members). The recipient side is a DIRECT FamilyMembership in the
        // connection's TargetFamilyId with Role = 'Owner'
        // (fm.FamilyId = c.TargetFamilyId AND fm.UserId = @UserId
        //  AND fm.Role = 'Owner').
        //
        // This is the one-hop rule: a user sees Source S only if they are an
        // Owner of the Target T of an active S→T connection with a granted
        // permission. Seeing T via sharing does NOT make the user an Owner of
        // T, so it cannot unlock a further hop. Sharing is non-transitive.
        var sharedRows = (await connection.QueryAsync<SharedRow>("""
            SELECT
                c.SourceFamilyId,
                MAX(CASE WHEN sp.PermissionKind = 'ViewAll'      AND sp.IsGranted = 1 THEN 1 ELSE 0 END) AS ViewAll,
                MAX(CASE WHEN sp.PermissionKind = 'ViewAdults'   AND sp.IsGranted = 1 THEN 1 ELSE 0 END) AS ViewAdults,
                MAX(CASE WHEN sp.PermissionKind = 'ViewChildren' AND sp.IsGranted = 1 THEN 1 ELSE 0 END) AS ViewChildren
            FROM FamilyConnections c
            -- The Source family must not be soft-deleted: a deleted family's
            -- data must never be visible via sharing.
            JOIN Families src
                ON src.Id = c.SourceFamilyId
               AND src.IsDeleted = 0
            -- The Target family must not be soft-deleted either: a share into
            -- a deleted family grants nothing.
            JOIN Families tgt
                ON tgt.Id = c.TargetFamilyId
               AND tgt.IsDeleted = 0
            JOIN FamilyMemberships fm
                ON fm.FamilyId = c.TargetFamilyId
               AND fm.UserId = @UserId
               AND fm.Role = 'Owner'
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
    ///
    /// The family set is exactly the access-map keys: membership families
    /// plus the Sources of direct one-hop shares into families the user is
    /// an Owner of. Share grants apply to target-family Owners only (not all
    /// target members). Sharing is non-transitive — no family reached only
    /// via a chain of connections is ever included.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetVisiblePersonIdsAsync(string userId)
    {
        var map = await GetAccessMapAsync(userId);
        if (map.Count == 0) return Array.Empty<string>();

        using var connection = _factory.Create();

        // Step 4: people in access-map families (membership ∪ direct-share
        // sources). No transitive families are in map.Keys by construction.
        // The Families join is belt-and-braces: map.Keys already excludes
        // soft-deleted families, but this guarantees a deleted family's
        // people can never leak even if the map were ever built differently.
        var rows = (await connection.QueryAsync<PersonRow>("""
            SELECT p.Id, pfm.FamilyId, pfm.MembershipKind
            FROM People p
            JOIN PersonFamilyMembership pfm
                ON pfm.PersonId = p.Id
               AND pfm.IsActive = 1
            JOIN Families f
                ON f.Id = pfm.FamilyId
               AND f.IsDeleted = 0
            WHERE p.IsDeleted = 0
              AND pfm.FamilyId IN @FamilyIds
            """,
            new { FamilyIds = map.Keys.ToList() })).ToList();

        // Step 5: filter shared families by MembershipKind. The kind filter
        // is derived from the direct connection only — never from a chain.
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
