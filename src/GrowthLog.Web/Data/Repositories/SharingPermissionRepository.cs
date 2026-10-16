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
    /// The unique index UX_SharingPermissions_Connection_Kind guarantees at
    /// most one row per (connection, kind), so this is a single
    /// INSERT ... ON CONFLICT DO UPDATE. There is no delete-then-insert and
    /// no "newest row wins" logic anywhere — the row's IsGranted value is
    /// the effective grant.
    /// </summary>
    public async Task SetAsync(
        string connectionId,
        string permissionKind,
        bool isGranted,
        string updatedByUserId)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO SharingPermissions
                (Id, FamilyConnectionId, PermissionKind, IsGranted, UpdatedByUserId, UpdatedUtc)
            VALUES
                (@Id, @ConnectionId, @PermissionKind, @IsGranted, @UpdatedByUserId, @UpdatedUtc)
            ON CONFLICT (FamilyConnectionId, PermissionKind) DO UPDATE SET
                IsGranted      = excluded.IsGranted,
                UpdatedByUserId = excluded.UpdatedByUserId,
                UpdatedUtc     = excluded.UpdatedUtc
            """,
            new
            {
                Id = Guid.NewGuid().ToString("d"),
                ConnectionId = connectionId,
                PermissionKind = permissionKind,
                IsGranted = isGranted ? 1 : 0,
                UpdatedByUserId = updatedByUserId,
                UpdatedUtc = DateTime.UtcNow.ToString("o")
            });
    }
}
