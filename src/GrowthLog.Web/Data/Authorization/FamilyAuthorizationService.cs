using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Authorization;

/// <summary>
/// Centralized server-side authorization for family data.
///
/// Visibility is delegated entirely to <see cref="FamilyVisibilityService"/>,
/// which is the single source of truth. This class only adds the mutation
/// rules (who can edit a person / measurement / sharing settings).
///
/// Mutation rules:
///   - A user can mutate a person if they hold Owner/Adult in a family the
///     person belongs to, or if the person is linked to their own account.
///   - Sharing settings can only be changed by an Owner of the source family.
/// </summary>
public class FamilyAuthorizationService
{
    private readonly IDbConnectionFactory _factory;
    private readonly FamilyVisibilityService _visibility;

    public FamilyAuthorizationService(
        IDbConnectionFactory factory,
        FamilyVisibilityService visibility)
    {
        _factory = factory;
        _visibility = visibility;
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
    /// Delegates to <see cref="FamilyVisibilityService"/>.
    /// </summary>
    public Task<IReadOnlyCollection<string>> GetVisiblePersonIdsAsync(string userId)
        => _visibility.GetVisiblePersonIdsAsync(userId);

    public Task<bool> CanViewPersonAsync(string userId, string personId)
        => _visibility.CanViewPersonAsync(userId, personId);

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
