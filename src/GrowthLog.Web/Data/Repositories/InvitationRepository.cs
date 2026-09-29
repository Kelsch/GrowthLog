using System.Security.Cryptography;
using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class InvitationRepository
{
    private readonly IDbConnectionFactory _factory;

    public InvitationRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Invitation?> GetByTokenAsync(string token)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<Invitation>(
            "SELECT * FROM Invitations WHERE Token = @Token",
            new { Token = token });
    }

    public async Task<IReadOnlyList<InvitationView>> GetForFamilyAsync(string familyId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<InvitationView>("""
            SELECT
                i.Id, i.Token, i.FamilyId, f.Name AS FamilyName,
                i.InvitedEmail, i.IntendedRole, i.Status,
                i.CreatedUtc, i.ExpiresUtc
            FROM Invitations i
            JOIN Families f ON f.Id = i.FamilyId
            WHERE i.FamilyId = @FamilyId
            ORDER BY i.CreatedUtc DESC
            """,
            new { FamilyId = familyId });
        return rows.ToList();
    }

    public async Task<Invitation> CreateAsync(
        string familyId,
        string? invitedEmail,
        string intendedRole,
        string invitedByUserId,
        TimeSpan? lifetime = null)
    {
        var invitation = new Invitation
        {
            Token = GenerateToken(),
            FamilyId = familyId,
            InvitedEmail = invitedEmail,
            IntendedRole = intendedRole,
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatuses.Pending,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
            ExpiresUtc = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(14)).ToString("o")
        };

        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO Invitations
                (Id, Token, FamilyId, InvitedEmail, IntendedRole, InvitedByUserId,
                 Status, CreatedUtc, ExpiresUtc, AcceptedUtc, AcceptedByUserId)
            VALUES
                (@Id, @Token, @FamilyId, @InvitedEmail, @IntendedRole, @InvitedByUserId,
                 @Status, @CreatedUtc, @ExpiresUtc, NULL, NULL)
            """, invitation);

        return invitation;
    }

    /// <summary>
    /// Accept an invitation atomically: mark it accepted and add the user
    /// to the family with the intended role. Returns false if the invitation
    /// is not pending or has expired.
    /// </summary>
    public async Task<bool> AcceptAsync(string token, string userId)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        var invitation = await connection.QuerySingleOrDefaultAsync<Invitation>(
            "SELECT * FROM Invitations WHERE Token = @Token",
            new { Token = token }, tx);

        if (invitation is null || invitation.Status != InvitationStatuses.Pending)
        {
            tx.Rollback();
            return false;
        }

        if (DateTime.TryParse(invitation.ExpiresUtc, out var expires) && expires < DateTime.UtcNow)
        {
            await connection.ExecuteAsync(
                "UPDATE Invitations SET Status = 'Expired' WHERE Id = @Id",
                new { Id = invitation.Id }, tx);
            tx.Commit();
            return false;
        }

        // If the user is already a member, just mark the invitation accepted.
        var existing = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Role FROM FamilyMemberships WHERE FamilyId = @FamilyId AND UserId = @UserId",
            new { FamilyId = invitation.FamilyId, UserId = userId }, tx);

        if (existing is null)
        {
            await connection.ExecuteAsync("""
                INSERT INTO FamilyMemberships (Id, FamilyId, UserId, Role, JoinedUtc)
                VALUES (@Id, @FamilyId, @UserId, @Role, @JoinedUtc)
                """,
                new
                {
                    Id = Guid.NewGuid().ToString("d"),
                    FamilyId = invitation.FamilyId,
                    UserId = userId,
                    Role = invitation.IntendedRole,
                    JoinedUtc = DateTime.UtcNow.ToString("o")
                }, tx);
        }

        await connection.ExecuteAsync("""
            UPDATE Invitations
            SET Status = 'Accepted',
                AcceptedUtc = @AcceptedUtc,
                AcceptedByUserId = @UserId
            WHERE Id = @Id
            """,
            new
            {
                Id = invitation.Id,
                AcceptedUtc = DateTime.UtcNow.ToString("o"),
                UserId = userId
            }, tx);

        tx.Commit();
        return true;
    }

    public async Task RejectAsync(string token)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            UPDATE Invitations
            SET Status = 'Rejected'
            WHERE Token = @Token AND Status = 'Pending'
            """,
            new { Token = token });
    }

    private static string GenerateToken()
    {
        // 32 bytes of cryptographically secure randomness, URL-safe base64.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
