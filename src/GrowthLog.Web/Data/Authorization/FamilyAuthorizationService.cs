using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Authorization;

/// <summary>
/// Centralized server-side authorization for family data.
/// Phase 2 covers membership and mutation checks; Phase 4 adds sharing.
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
}
