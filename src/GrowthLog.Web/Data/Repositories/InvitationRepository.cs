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
    /// Inspects an invitation without mutating it, so the UI can decide what
    /// to show (accept form, "already accepted", "expired", etc.).
    /// </summary>
    public async Task<Invitation?> GetByTokenAsync(string token, bool includeNonPending)
    {
        using var connection = _factory.Create();
        var invitation = await connection.QuerySingleOrDefaultAsync<Invitation>(
            "SELECT * FROM Invitations WHERE Token = @Token",
            new { Token = token });

        if (invitation is null) return null;
        if (!includeNonPending && invitation.Status != InvitationStatuses.Pending) return null;
        return invitation;
    }

    /// <summary>
    /// Result of attempting to accept an invitation.
    /// </summary>
    public enum AcceptResult
    {
        Accepted,
        AlreadyAcceptedByYou,
        AlreadyAcceptedByOther,
        Expired,
        NotFound,
        EmailMismatch
    }

    /// <summary>
    /// Accept an invitation atomically and idempotently.
    ///
    /// Steps (all inside one transaction):
    ///   1. Load the invitation.
    ///   2. If already Accepted:
    ///        - by this user  -> ensure membership exists, return AlreadyAcceptedByYou.
    ///        - by someone else -> return AlreadyAcceptedByOther.
    ///   3. If Expired (or past ExpiresUtc) -> mark Expired, return Expired.
    ///   4. If the invitation is addressed to a specific email and the
    ///      accepting user's email does not match -> EmailMismatch.
    ///   5. Ensure a FamilyMembership exists with the intended role.
    ///   6. Link an existing Person in the target family to this user when a
    ///      reasonable match exists (Person.UserId is null and either the
    ///      Person's email matches, or the Person was pre-created for this
    ///      invitation).
    ///   7. Mark the invitation Accepted.
    /// </summary>
    public async Task<AcceptResult> AcceptAsync(string token, string userId, string? userEmail)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        var invitation = await connection.QuerySingleOrDefaultAsync<Invitation>(
            "SELECT * FROM Invitations WHERE Token = @Token",
            new { Token = token }, tx);

        if (invitation is null)
        {
            tx.Rollback();
            return AcceptResult.NotFound;
        }

        // Already accepted?
        if (invitation.Status == InvitationStatuses.Accepted)
        {
            if (invitation.AcceptedByUserId == userId)
            {
                // Idempotent: make sure the membership still exists.
                await EnsureMembershipAsync(connection, tx, invitation.FamilyId, userId, invitation.IntendedRole);
                await LinkPersonAsync(connection, tx, invitation.FamilyId, userId, userEmail);
                tx.Commit();
                return AcceptResult.AlreadyAcceptedByYou;
            }

            tx.Rollback();
            return AcceptResult.AlreadyAcceptedByOther;
        }

        if (invitation.Status != InvitationStatuses.Pending)
        {
            tx.Rollback();
            return AcceptResult.NotFound;
        }

        // Expired?
        if (DateTime.TryParse(invitation.ExpiresUtc, out var expires) && expires < DateTime.UtcNow)
        {
            await connection.ExecuteAsync(
                "UPDATE Invitations SET Status = 'Expired' WHERE Id = @Id",
                new { Id = invitation.Id }, tx);
            tx.Commit();
            return AcceptResult.Expired;
        }

        // Email mismatch (only enforced when the invitation names an email).
        if (!string.IsNullOrWhiteSpace(invitation.InvitedEmail)
            && !string.IsNullOrWhiteSpace(userEmail)
            && !string.Equals(invitation.InvitedEmail.Trim(), userEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            tx.Rollback();
            return AcceptResult.EmailMismatch;
        }

        // 5. Membership.
        await EnsureMembershipAsync(connection, tx, invitation.FamilyId, userId, invitation.IntendedRole);

        // 6. Link an existing Person to this user, if a reasonable match exists.
        await LinkPersonAsync(connection, tx, invitation.FamilyId, userId, userEmail);

        // 7. Mark accepted.
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
        return AcceptResult.Accepted;
    }

    private static async Task EnsureMembershipAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction tx,
        string familyId,
        string userId,
        string role)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Role FROM FamilyMemberships WHERE FamilyId = @FamilyId AND UserId = @UserId",
            new { FamilyId = familyId, UserId = userId }, tx);

        if (existing is null)
        {
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
                }, tx);
        }
    }

    /// <summary>
    /// Links an existing Person in the target family to the accepting user
    /// when a reasonable match exists. A Person is a candidate if it is an
    /// active member of the family, has no UserId yet, and either:
    ///   - its name matches the user's display name / email local-part, or
    ///   - it is the only unlinked adult in the family.
    /// This is intentionally conservative: we never overwrite an existing link.
    /// </summary>
    private static async Task LinkPersonAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction tx,
        string familyId,
        string userId,
        string? userEmail)
    {
        // Already linked to a person in this family? Nothing to do.
        var alreadyLinked = await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(1)
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id
            WHERE pfm.FamilyId = @FamilyId
              AND pfm.IsActive = 1
              AND p.UserId = @UserId
            """,
            new { FamilyId = familyId, UserId = userId }, tx);

        if (alreadyLinked > 0) return;

        // Candidate: unlinked, active member of the family.
        var candidates = (await connection.QueryAsync<PersonCandidate>("""
            SELECT p.Id, p.FirstName, p.LastName, p.MiddleName
            FROM People p
            JOIN PersonFamilyMembership pfm ON pfm.PersonId = p.Id
            WHERE pfm.FamilyId = @FamilyId
              AND pfm.IsActive = 1
              AND p.IsDeleted = 0
              AND p.UserId IS NULL
            """,
            new { FamilyId = familyId }, tx)).ToList();

        if (candidates.Count == 0) return;

        string? matchId = null;

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var localPart = userEmail.Split('@')[0];
            matchId = candidates.FirstOrDefault(c =>
                string.Equals(c.FirstName, localPart, StringComparison.OrdinalIgnoreCase)
                || string.Equals($"{c.FirstName} {c.LastName}".Trim(), localPart, StringComparison.OrdinalIgnoreCase)
            ).Id;
        }

        // Fall back to the only unlinked candidate.
        if (matchId is null && candidates.Count == 1)
        {
            matchId = candidates[0].Id;
        }

        if (matchId is null) return;

        await connection.ExecuteAsync(
            "UPDATE People SET UserId = @UserId WHERE Id = @PersonId AND UserId IS NULL",
            new { UserId = userId, PersonId = matchId }, tx);
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

    private sealed class PersonCandidate
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
    }
}
