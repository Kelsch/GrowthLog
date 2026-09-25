using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

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
}
