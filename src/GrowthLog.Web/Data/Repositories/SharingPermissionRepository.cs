using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class SharingPermissionRepository
{
    private readonly IDbConnectionFactory _factory;

    public SharingPermissionRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<SharingPermission>> GetForConnectionAsync(string connectionId)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryAsync<SharingPermission>(
            "SELECT * FROM SharingPermissions WHERE FamilyConnectionId = @ConnectionId",
            new { ConnectionId = connectionId });
        return rows.ToList();
    }

    /// <summary>
    /// Upsert a permission grant/revoke for a connection.
    ///
    /// This is written so that the *effective* grant for a
    /// (connection, kind) pair is always the row with the newest
    /// UpdatedUtc. It does not rely on a unique index existing: if
    /// duplicate rows are present (e.g. from an older schema), the
    /// authorization SQL picks the newest row via the "no newer revoke"
    /// check, so a stale IsGranted = 1 row can never win over a newer
    /// IsGranted = 0 row.
    ///
    /// We still attempt the ON CONFLICT upsert first (fast path when the
    /// unique index exists), then defensively delete any older duplicate
    /// rows for the same (connection, kind) so the table stays clean.
    /// </summary>
    public async Task SetAsync(
        string connectionId,
        string permissionKind,
        bool isGranted,
        string updatedByUserId)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        var now = DateTime.UtcNow.ToString("o");

        await connection.ExecuteAsync("""
            INSERT INTO SharingPermissions
                (Id, FamilyConnectionId, PermissionKind, IsGranted, UpdatedByUserId, UpdatedUtc)
            VALUES
                (@Id, @ConnectionId, @PermissionKind, @IsGranted, @UpdatedByUserId, @UpdatedUtc)
            ON CONFLICT (FamilyConnectionId, PermissionKind)
            DO UPDATE SET
                IsGranted = excluded.IsGranted,
                UpdatedByUserId = excluded.UpdatedByUserId,
                UpdatedUtc = excluded.UpdatedUtc
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                ConnectionId = connectionId,
                PermissionKind = permissionKind,
                IsGranted = isGranted ? 1 : 0,
                UpdatedByUserId = updatedByUserId,
                UpdatedUtc = now
            }, tx);

        // Defensive cleanup: if the unique index is missing, the upsert above
        // inserted a new row instead of updating. Remove every older row for
        // the same (connection, kind) so only the newest remains.
        await connection.ExecuteAsync("""
            DELETE FROM SharingPermissions
            WHERE FamilyConnectionId = @ConnectionId
              AND PermissionKind = @PermissionKind
              AND UpdatedUtc < @Now
            """,
            new { ConnectionId = connectionId, PermissionKind = permissionKind, Now = now }, tx);

        tx.Commit();
    }
}
