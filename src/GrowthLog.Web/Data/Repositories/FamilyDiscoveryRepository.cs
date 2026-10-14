using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

/// <summary>
/// A user the current user shares at least one family membership with.
/// This is the ONLY discovery entry point for connecting families — we never
/// list all users or all families in the system.
/// </summary>
public class CoMemberRow
{
    public string UserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    /// <summary>Names of the families the two users share, for context in the UI.</summary>
    public string SharedFamilyNames { get; set; } = string.Empty;
}

/// <summary>
/// A family owned/administered by another user that the current user could
/// connect to. Excludes families the current user already belongs to and
/// families already connected in either direction.
/// </summary>
public class ConnectableFamilyRow
{
    public string FamilyId { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty;
}

/// <summary>
/// Private discovery for the family-connect flow.
///
/// Discovery is limited to users who already share at least one family
/// membership with the current user (co-members). There is no global listing
/// of users or families. Connecting a family does NOT grant visibility by
/// itself — a SharingPermission must still be granted (see
/// FamilyAuthorizationService.VisiblePeopleSql rule 4).
///
/// Direction convention (matches FamilyConnection):
///   SourceFamilyId = the family that OWNS the data being shared out.
///   TargetFamilyId = the family that RECEIVES visibility.
/// </summary>
public class FamilyDiscoveryRepository
{
    private readonly IDbConnectionFactory _factory;

    public FamilyDiscoveryRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Returns distinct users who share at least one family membership with
    /// the current user, along with the names of the shared families.
    /// Scoped to @UserId in SQL — no global user list.
    /// </summary>
    public async Task<IReadOnlyList<CoMemberRow>> GetCoMembersAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<CoMemberRow>("""
            SELECT
                fm2.UserId AS UserId,
                u.DisplayName AS DisplayName,
                u.Email AS Email,
                GROUP_CONCAT(DISTINCT f.Name) AS SharedFamilyNames
            FROM FamilyMemberships fm1
            JOIN FamilyMemberships fm2
                ON fm2.FamilyId = fm1.FamilyId
               AND fm2.UserId <> fm1.UserId
            JOIN Families f
                ON f.Id = fm1.FamilyId
               AND f.IsDeleted = 0
            LEFT JOIN AspNetUsers u ON u.Id = fm2.UserId
            WHERE fm1.UserId = @UserId
            GROUP BY fm2.UserId, u.DisplayName, u.Email
            ORDER BY u.DisplayName, u.Email
            """,
            new { UserId = userId });
        return rows.ToList();
    }

    /// <summary>
    /// Returns families owned/administered by <paramref name="otherUserId"/>
    /// that the current user is NOT a member of and has NO existing connection
    /// to (in either direction). This is what the current user can connect to.
    /// </summary>
    public async Task<IReadOnlyList<ConnectableFamilyRow>> GetConnectableFamiliesAsync(
        string userId,
        string otherUserId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<ConnectableFamilyRow>("""
            SELECT
                f.Id AS FamilyId,
                f.Name AS FamilyName,
                fm.UserId AS OwnerUserId,
                fm.Role AS OwnerRole
            FROM Families f
            JOIN FamilyMemberships fm
                ON fm.FamilyId = f.Id
               AND fm.UserId = @OtherUserId
               AND fm.Role IN ('Owner', 'Adult')
            WHERE f.IsDeleted = 0
              -- Current user is not already a member of this family.
              AND NOT EXISTS (
                    SELECT 1 FROM FamilyMemberships mine
                    WHERE mine.FamilyId = f.Id AND mine.UserId = @UserId
              )
              -- No existing connection in either direction between any of the
              -- current user's families and this family.
              AND NOT EXISTS (
                    SELECT 1
                    FROM FamilyConnections c
                    JOIN FamilyMemberships mine2
                        ON mine2.UserId = @UserId
                       AND (c.SourceFamilyId = mine2.FamilyId OR c.TargetFamilyId = mine2.FamilyId)
                    WHERE (c.SourceFamilyId = f.Id OR c.TargetFamilyId = f.Id)
              )
            ORDER BY f.Name
            """,
            new { UserId = userId, OtherUserId = otherUserId });
        return rows.ToList();
    }

    /// <summary>
    /// Reverse view: families the current user owns/admins that could be
    /// connected toward <paramref name="otherUserId"/>'s families. Used to
    /// present the "Share [My family] with [Their family]" direction.
    /// </summary>
    public async Task<IReadOnlyList<ConnectableFamilyRow>> GetMyConnectableFamiliesAsync(
        string userId,
        string otherUserId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<ConnectableFamilyRow>("""
            SELECT
                f.Id AS FamilyId,
                f.Name AS FamilyName,
                fm.UserId AS OwnerUserId,
                fm.Role AS OwnerRole
            FROM Families f
            JOIN FamilyMemberships fm
                ON fm.FamilyId = f.Id
               AND fm.UserId = @UserId
               AND fm.Role IN ('Owner', 'Adult')
            WHERE f.IsDeleted = 0
              AND NOT EXISTS (
                    SELECT 1 FROM FamilyMemberships theirs
                    WHERE theirs.FamilyId = f.Id AND theirs.UserId = @OtherUserId
              )
              AND NOT EXISTS (
                    SELECT 1
                    FROM FamilyConnections c
                    JOIN FamilyMemberships theirs2
                        ON theirs2.UserId = @OtherUserId
                       AND (c.SourceFamilyId = theirs2.FamilyId OR c.TargetFamilyId = theirs2.FamilyId)
                    WHERE (c.SourceFamilyId = f.Id OR c.TargetFamilyId = f.Id)
              )
            ORDER BY f.Name
            """,
            new { UserId = userId, OtherUserId = otherUserId });
        return rows.ToList();
    }
}
