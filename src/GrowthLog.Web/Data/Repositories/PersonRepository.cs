using Dapper;
using GrowthLog.Web.Data.Authorization;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class PersonRepository
{
    private readonly IDbConnectionFactory _factory;

    public PersonRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Returns all people the user is authorized to see, across every family
    /// they belong to plus any connected families that have granted sharing.
    /// Authorization is enforced in SQL — no post-filtering.
    /// </summary>
    public async Task<IReadOnlyList<Person>> GetVisibleForUserAsync(string userId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>($"""
            SELECT p.*
            FROM People p
            WHERE p.Id IN ({FamilyAuthorizationService.VisiblePeopleSql})
            ORDER BY p.FirstName, p.LastName
            """,
            new { UserId = userId });
        return rows.ToList();
    }

    /// <summary>
    /// Returns people visible to the user who are members of the given family.
    /// </summary>
    public async Task<IReadOnlyList<Person>> GetVisibleForUserInFamilyAsync(string userId, string familyId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<Person>($"""
            SELECT p.*
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id
            WHERE pfm.FamilyId = @FamilyId
              AND pfm.IsActive = 1
              AND p.Id IN ({FamilyAuthorizationService.VisiblePeopleSql})
            ORDER BY p.FirstName, p.LastName
            """,
            new { UserId = userId, FamilyId = familyId });
        return rows.ToList();
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
