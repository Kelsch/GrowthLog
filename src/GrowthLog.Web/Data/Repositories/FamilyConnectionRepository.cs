using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class FamilyConnectionRepository
{
    private readonly IDbConnectionFactory _factory;

    public FamilyConnectionRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<FamilyConnection?> GetByIdAsync(string id)
    {
        using var connection = _factory.Create();
        return await connection.QuerySingleOrDefaultAsync<FamilyConnection>(
            "SELECT * FROM FamilyConnections WHERE Id = @Id",
            new { Id = id });
    }

    /// <summary>
    /// Connections where the given family is the source (i.e. the family
    /// that owns the data being shared out).
    /// </summary>
    /// <summary>
    /// Connections where the given family is the source (i.e. the family
    /// that owns the data being shared out), with the permission state for
    /// each kind.
    ///
    /// The unique index UX_SharingPermissions_Connection_Kind guarantees at
    /// most one row per (connection, kind), so a simple LEFT JOIN is enough
    /// — no "newest row wins" subquery is needed. This matches the
    /// authorization SQL exactly, so the checkbox state shown in the UI is
    /// the same state the visibility queries use.
    /// </summary>
    public async Task<IReadOnlyList<FamilyConnectionView>> GetOutgoingViewsAsync(string sourceFamilyId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<FamilyConnectionView>("""
            SELECT
                c.Id,
                c.SourceFamilyId,
                c.TargetFamilyId,
                t.Name AS TargetFamilyName,
                c.Label,
                c.IsActive,
                COALESCE(vc.IsGranted, 0) AS ViewChildren,
                COALESCE(va.IsGranted, 0) AS ViewAdults,
                COALESCE(vall.IsGranted, 0) AS ViewAll
            FROM FamilyConnections c
            JOIN Families t ON t.Id = c.TargetFamilyId
            LEFT JOIN SharingPermissions vc
                ON vc.FamilyConnectionId = c.Id
               AND vc.PermissionKind = 'ViewChildren'
            LEFT JOIN SharingPermissions va
                ON va.FamilyConnectionId = c.Id
               AND va.PermissionKind = 'ViewAdults'
            LEFT JOIN SharingPermissions vall
                ON vall.FamilyConnectionId = c.Id
               AND vall.PermissionKind = 'ViewAll'
            WHERE c.SourceFamilyId = @SourceFamilyId
            ORDER BY t.Name
            """,
            new { SourceFamilyId = sourceFamilyId });
        return rows.ToList();
    }

    public async Task<string> CreateAsync(
        string sourceFamilyId,
        string targetFamilyId,
        string? label,
        string createdByUserId)
    {
        // Defense in depth: the DB CHECK constraint also enforces this.
        if (sourceFamilyId == targetFamilyId)
        {
            throw new InvalidOperationException("A family cannot be connected to itself.");
        }

        using var connection = _factory.Create();
        var id = Guid.NewGuid().ToString("d");
        await connection.ExecuteAsync("""
            INSERT INTO FamilyConnections
                (Id, SourceFamilyId, TargetFamilyId, Label, IsActive, CreatedByUserId, CreatedUtc)
            VALUES
                (@Id, @SourceFamilyId, @TargetFamilyId, @Label, 1, @CreatedByUserId, @CreatedUtc)
            """,
            new
            {
                Id = id,
                SourceFamilyId = sourceFamilyId,
                TargetFamilyId = targetFamilyId,
                Label = label,
                CreatedByUserId = createdByUserId,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            });
        return id;
    }

    public async Task SetActiveAsync(string connectionId, bool isActive)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync(
            "UPDATE FamilyConnections SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = connectionId, IsActive = isActive ? 1 : 0 });
    }

    public async Task DeleteAsync(string connectionId)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();
        await connection.ExecuteAsync(
            "DELETE FROM SharingPermissions WHERE FamilyConnectionId = @Id",
            new { Id = connectionId }, tx);
        await connection.ExecuteAsync(
            "DELETE FROM FamilyConnections WHERE Id = @Id",
            new { Id = connectionId }, tx);
        tx.Commit();
    }
}
