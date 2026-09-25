using Dapper;
using GrowthLog.Web.Models;

namespace GrowthLog.Web.Data.Repositories;

public class AuditRepository
{
    private readonly IDbConnectionFactory _factory;

    public AuditRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task WriteAsync(
        string? userId,
        string action,
        string entityType,
        string entityId,
        string? detailsJson = null)
    {
        using var connection = _factory.Create();
        await connection.ExecuteAsync("""
            INSERT INTO AuditLog (OccurredUtc, UserId, Action, EntityType, EntityId, DetailsJson)
            VALUES (@OccurredUtc, @UserId, @Action, @EntityType, @EntityId, @DetailsJson)
            """,
            new
            {
                OccurredUtc = DateTime.UtcNow.ToString("o"),
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                DetailsJson = detailsJson
            });
    }
}
