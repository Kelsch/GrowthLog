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
    /// The effective grant for a (connection, kind) pair is the single row
    /// that exists for that pair. To guarantee that, this method:
    ///
    ///   1. Deletes EVERY existing row for the (connection, kind) pair.
    ///   2. Inserts exactly one fresh row with the new IsGranted value.
    ///
    /// Both steps run in one transaction. This is deliberately not an
    /// ON CONFLICT upsert: that only works when a unique index exists on
    /// (FamilyConnectionId, PermissionKind), and if it is missing the upsert
    /// silently inserts a duplicate row — leaving a stale IsGranted = 1 row
    /// that keeps granting access after the user unchecks the box.
    ///
    /// Deleting all rows (not just "older" ones) also fixes the case where
    /// two toggles happen within the same millisecond and UpdatedUtc strings
    /// compare equal, which previously let a stale grant survive.
    /// </summary>
    public async Task SetAsync(
        string connectionId,
        string permissionKind,
        bool isGranted,
        string updatedByUserId)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        // 1. Remove every existing row for this (connection, kind) pair so
        //    exactly one row remains after the insert below.
        await connection.ExecuteAsync("""
            DELETE FROM SharingPermissions
            WHERE FamilyConnectionId = @ConnectionId
              AND PermissionKind = @PermissionKind
            """,
            new { ConnectionId = connectionId, PermissionKind = permissionKind }, tx);

        // 2. Insert the single authoritative row.
        await connection.ExecuteAsync("""
            INSERT INTO SharingPermissions
                (Id, FamilyConnectionId, PermissionKind, IsGranted, UpdatedByUserId, UpdatedUtc)
            VALUES
                (@Id, @ConnectionId, @PermissionKind, @IsGranted, @UpdatedByUserId, @UpdatedUtc)
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                ConnectionId = connectionId,
                PermissionKind = permissionKind,
                IsGranted = isGranted ? 1 : 0,
                UpdatedByUserId = updatedByUserId,
                UpdatedUtc = DateTime.UtcNow.ToString("o")
            }, tx);

        tx.Commit();
    }
}
