using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class PersonRepository
{
    private readonly IDbConnectionFactory _factory;

    public PersonRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Person?> GetByIdAsync(string id)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<Person>(
            "SELECT * FROM People WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id });
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
            INSERT INTO People (Id, UserId, FirstName, MiddleName, LastName, DateOfBirth, AvatarUrl, CreatedUtc, IsDeleted)
            VALUES (@Id, @UserId, @FirstName, @MiddleName, @LastName, @DateOfBirth, @AvatarUrl, @CreatedUtc, 0)
            """, person);
        return person.Id;
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
