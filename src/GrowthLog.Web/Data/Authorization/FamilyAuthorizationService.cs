using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Authorization;

/// <summary>
/// Centralized server-side authorization for family data.
///
/// Visibility rules (Phase 4):
///   1. A user can see any person who is an active member of a family the
///      user belongs to (any role).
///   2. A user can see a person linked to their own account (People.UserId).
///   3. A user can see a person who is a child in a family the user belongs
///      to (parental rule — covered by rule 1).
///   4. A user can see a person in a *connected* family if the source family
///      has granted a matching SharingPermission (ViewChildren / ViewAdults /
///      ViewAll) on the connection.
///
/// Mutation rules:
///   - A user can mutate a person if they hold Owner/Adult in a family the
///     person belongs to, or if the person is linked to their own account.
///   - Sharing settings can only be changed by an Owner of the source family.
/// </summary>
public class FamilyAuthorizationService
{
    private readonly IDbConnectionFactory _factory;

    public FamilyAuthorizationService(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<string?> GetRoleAsync(string userId, string familyId)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Role FROM FamilyMemberships WHERE UserId = @UserId AND FamilyId = @FamilyId",
            new { UserId = userId, FamilyId = familyId });
    }

    public async Task<bool> CanViewFamilyAsync(string userId, string familyId)
    {
        var role = await GetRoleAsync(userId, familyId);
        return role is not null;
    }

    public async Task<bool> CanMutateFamilyAsync(string userId, string familyId)
    {
        var role = await GetRoleAsync(userId, familyId);
        return role is not null && FamilyRoles.CanMutate(role);
    }

    public async Task<bool> CanAdministerFamilyAsync(string userId, string familyId)
    {
        var role = await GetRoleAsync(userId, familyId);
        return role is not null && FamilyRoles.CanAdminister(role);
    }

    /// <summary>
    /// Returns the set of person IDs the user is authorized to see.
    /// This is the single source of truth for visibility and is used by
    /// authorization-aware repository queries.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetVisiblePersonIdsAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<string>(VisiblePeopleSql, new { UserId = userId });
        return rows.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// SQL fragment that yields the IDs of people visible to @UserId.
    /// Kept as a constant so repositories can embed it directly in their
    /// own queries (authorization-aware filtering at the database level).
    ///
    /// Direction convention for FamilyConnections:
    ///   SourceFamilyId = the family that OWNS the data being shared out.
    ///   TargetFamilyId = the family that RECEIVES visibility.
    /// So a user gains shared visibility when they are a member of the
    /// Target family of an active connection whose Source family has a
    /// granted SharingPermission. Every active connection that matches is
    /// considered independently — there is no "first match wins" behavior.
    /// </summary>
    public const string VisiblePeopleSql = """
        SELECT DISTINCT p.Id
        FROM People p
        WHERE p.IsDeleted = 0
          AND (
            -- Rule 1: person is an active member of a family the user belongs to.
            EXISTS (
                SELECT 1
                FROM PersonFamilyMembership pfm
                JOIN FamilyMemberships fm ON fm.FamilyId = pfm.FamilyId
                WHERE pfm.PersonId = p.Id
                  AND pfm.IsActive = 1
                  AND fm.UserId = @UserId
            )
            -- Rule 2: person is linked to the user's own account.
            OR p.UserId = @UserId
            -- Rule 3: person was created by the user. This keeps people the
            -- user created visible even when they have no active family
            -- membership (e.g. an orphaned/unassigned person).
            OR p.CreatedByUserId = @UserId
            -- Rule 4: person is in a Source family of an active connection
            -- whose Target family the user belongs to, and that connection
            -- has a granted permission matching the person's membership kind.
            OR EXISTS (
                SELECT 1
                FROM PersonFamilyMembership pfm2
                JOIN FamilyConnections c
                    ON c.SourceFamilyId = pfm2.FamilyId
                   AND c.IsActive = 1
                JOIN FamilyMemberships fm2
                    ON fm2.FamilyId = c.TargetFamilyId
                   AND fm2.UserId = @UserId
                JOIN SharingPermissions sp
                    ON sp.FamilyConnectionId = c.Id
                   AND sp.IsGranted = 1
                WHERE pfm2.PersonId = p.Id
                  AND pfm2.IsActive = 1
                  AND (
                        sp.PermissionKind = 'ViewAll'
                     OR (sp.PermissionKind = 'ViewChildren' AND pfm2.MembershipKind = 'Child')
                     OR (sp.PermissionKind = 'ViewAdults'   AND pfm2.MembershipKind = 'Adult')
                  )
            )
          )
        """;

    public async Task<bool> CanViewPersonAsync(string userId, string personId)
    {
        using var connection = _factory.Create();
        var count = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM ({VisiblePeopleSql}) v WHERE v.Id = @PersonId",
            new { UserId = userId, PersonId = personId });
        return count > 0;
    }

    /// <summary>
    /// A user may mutate a person if they hold a mutating role in any family
    /// the person is an active member of, or if they are the person's linked user.
    /// </summary>
    public async Task<bool> CanMutatePersonAsync(string userId, string personId)
    {
        using var connection = _factory.Create();

        var linkedUserId = await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT UserId FROM People WHERE Id = @Id AND IsDeleted = 0",
            new { Id = personId });

        if (linkedUserId is not null && linkedUserId == userId)
        {
            return true;
        }

        var count = await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(1)
            FROM PersonFamilyMembership pfm
            JOIN FamilyMemberships fm ON fm.FamilyId = pfm.FamilyId
            WHERE pfm.PersonId = @PersonId
              AND pfm.IsActive = 1
              AND fm.UserId = @UserId
              AND fm.Role IN ('Owner', 'Adult')
            """,
            new { PersonId = personId, UserId = userId });

        return count > 0;
    }

    /// <summary>
    /// A user may view a measurement if they can view the person it belongs to.
    /// </summary>
    public async Task<bool> CanViewMeasurementAsync(string userId, string measurementId)
    {
        using var connection = _factory.Create();
        var personId = await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT PersonId FROM Measurements WHERE Id = @Id AND IsDeleted = 0",
            new { Id = measurementId });

        if (personId is null) return false;
        return await CanViewPersonAsync(userId, personId);
    }

    /// <summary>
    /// A user may mutate a measurement if they can mutate the person it belongs to.
    /// </summary>
    public async Task<bool> CanMutateMeasurementAsync(string userId, string measurementId)
    {
        using var connection = _factory.Create();
        var personId = await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT PersonId FROM Measurements WHERE Id = @Id AND IsDeleted = 0",
            new { Id = measurementId });

        if (personId is null) return false;
        return await CanMutatePersonAsync(userId, personId);
    }

    /// <summary>
    /// Only an Owner of the source family may change sharing settings.
    /// </summary>
    public async Task<bool> CanManageSharingAsync(string userId, string sourceFamilyId)
    {
        var role = await GetRoleAsync(userId, sourceFamilyId);
        return role == FamilyRoles.Owner;
    }
}
