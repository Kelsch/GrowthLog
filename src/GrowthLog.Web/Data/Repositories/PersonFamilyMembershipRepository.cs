using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class PersonFamilyMembershipRepository
{
    private readonly IDbConnectionFactory _factory;

    public PersonFamilyMembershipRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<PersonFamilyMembership>> GetForPersonAsync(string personId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<PersonFamilyMembership>(
            "SELECT * FROM PersonFamilyMembership WHERE PersonId = @PersonId",
            new { PersonId = personId });
        return rows.ToList();
    }

    public async Task<PersonFamilyMembership?> GetAsync(string personId, string familyId)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<PersonFamilyMembership>(
            "SELECT * FROM PersonFamilyMembership WHERE PersonId = @PersonId AND FamilyId = @FamilyId",
            new { PersonId = personId, FamilyId = familyId });
    }

    public async Task AddAsync(string personId, string familyId, string membershipKind)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO PersonFamilyMembership (Id, PersonId, FamilyId, MembershipKind, JoinedUtc, IsActive)
            VALUES (@Id, @PersonId, @FamilyId, @MembershipKind, @JoinedUtc, 1)
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                PersonId = personId,
                FamilyId = familyId,
                MembershipKind = membershipKind,
                JoinedUtc = DateTime.UtcNow.ToString("o")
            });
    }

    public async Task UpdateKindAsync(string personId, string familyId, string membershipKind)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE PersonFamilyMembership
            SET MembershipKind = @MembershipKind
            WHERE PersonId = @PersonId AND FamilyId = @FamilyId
            """,
            new { PersonId = personId, FamilyId = familyId, MembershipKind = membershipKind });
    }

    /// <summary>Soft removal: sets IsActive = 0.</summary>
    public async Task DeactivateAsync(string personId, string familyId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE PersonFamilyMembership
            SET IsActive = 0
            WHERE PersonId = @PersonId AND FamilyId = @FamilyId
            """,
            new { PersonId = personId, FamilyId = familyId });
    }

    public async Task ReactivateAsync(string personId, string familyId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE PersonFamilyMembership
            SET IsActive = 1
            WHERE PersonId = @PersonId AND FamilyId = @FamilyId
            """,
            new { PersonId = personId, FamilyId = familyId });
    }
}
