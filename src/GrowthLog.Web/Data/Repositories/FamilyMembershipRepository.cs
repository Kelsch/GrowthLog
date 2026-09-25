using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class FamilyMembershipRepository
{
    private readonly IDbConnectionFactory _factory;

    public FamilyMembershipRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<FamilyMembership?> GetAsync(string familyId, string userId)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<FamilyMembership>(
            "SELECT * FROM FamilyMemberships WHERE FamilyId = @FamilyId AND UserId = @UserId",
            new { FamilyId = familyId, UserId = userId });
    }

    public async Task<IReadOnlyList<FamilyMembership>> GetForFamilyAsync(string familyId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<FamilyMembership>(
            "SELECT * FROM FamilyMemberships WHERE FamilyId = @FamilyId",
            new { FamilyId = familyId });
        return rows.ToList();
    }

    public async Task AddAsync(string familyId, string userId, string role)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO FamilyMemberships (Id, FamilyId, UserId, Role, JoinedUtc)
            VALUES (@Id, @FamilyId, @UserId, @Role, @JoinedUtc)
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                FamilyId = familyId,
                UserId = userId,
                Role = role,
                JoinedUtc = DateTime.UtcNow.ToString("o")
            });
    }

    public async Task UpdateRoleAsync(string familyId, string userId, string role)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE FamilyMemberships
            SET Role = @Role
            WHERE FamilyId = @FamilyId AND UserId = @UserId
            """,
            new { FamilyId = familyId, UserId = userId, Role = role });
    }

    public async Task RemoveAsync(string familyId, string userId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "DELETE FROM FamilyMemberships WHERE FamilyId = @FamilyId AND UserId = @UserId",
            new { FamilyId = familyId, UserId = userId });
    }
}
