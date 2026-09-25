using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class RelationshipRepository
{
    private readonly IDbConnectionFactory _factory;

    public RelationshipRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<PersonRelationship>> GetForPersonAsync(string personId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<PersonRelationship>("""
            SELECT * FROM PersonRelationships
            WHERE FromPersonId = @PersonId OR ToPersonId = @PersonId
            """,
            new { PersonId = personId });
        return rows.ToList();
    }

    public async Task AddParentChildAsync(string parentPersonId, string childPersonId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT OR IGNORE INTO PersonRelationships
                (Id, FromPersonId, ToPersonId, RelationshipType, CreatedUtc)
            VALUES (@Id, @FromPersonId, @ToPersonId, 'Parent', @CreatedUtc)
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                FromPersonId = parentPersonId,
                ToPersonId = childPersonId,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            });
    }

    public async Task AddSpouseAsync(string personAId, string personBId)
    {
        // Store once with canonical ordering so the pair is unique.
        var (from, to) = string.CompareOrdinal(personAId, personBId) <= 0
            ? (personAId, personBId)
            : (personBId, personAId);

        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT OR IGNORE INTO PersonRelationships
                (Id, FromPersonId, ToPersonId, RelationshipType, CreatedUtc)
            VALUES (@Id, @FromPersonId, @ToPersonId, 'Spouse', @CreatedUtc)
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                FromPersonId = from,
                ToPersonId = to,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            });
    }

    public async Task RemoveAsync(string relationshipId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "DELETE FROM PersonRelationships WHERE Id = @Id",
            new { Id = relationshipId });
    }
}
