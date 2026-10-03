using Dapper;

namespace GrowthLog.Web.Data;

/// <summary>
/// Applies plain SQL migration scripts from db/migrations in filename order,
/// recording applied versions in the SchemaVersions table.
/// </summary>
public sealed class MigrationRunner
{
    private readonly IDbConnectionFactory _factory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(
        IDbConnectionFactory factory,
        IWebHostEnvironment env,
        ILogger<MigrationRunner> logger)
    {
        _factory = factory;
        _env = env;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var migrationsDir = Path.Combine(_env.ContentRootPath, "db", "migrations");
        if (!Directory.Exists(migrationsDir))
        {
            // Fall back to the output directory (migrations are copied there).
            migrationsDir = Path.Combine(AppContext.BaseDirectory, "db", "migrations");
        }

        if (!Directory.Exists(migrationsDir))
        {
            _logger.LogWarning("Migrations directory not found: {Dir}", migrationsDir);
            return;
        }

        using var connection = _factory.Create();

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS SchemaVersions (
                Version    TEXT NOT NULL PRIMARY KEY,
                AppliedUtc TEXT NOT NULL
            );
            """);

        var applied = (await connection.QueryAsync<string>(
            "SELECT Version FROM SchemaVersions")).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var files = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var version = Path.GetFileNameWithoutExtension(file);
            if (applied.Contains(version))
            {
                continue;
            }

            _logger.LogInformation("Applying migration {Version}", version);
            var sql = await File.ReadAllTextAsync(file);

            using var tx = connection.BeginTransaction();
            try
            {
                await connection.ExecuteAsync(sql, transaction: tx);
            }
            catch (Exception ex) when (IsAlreadyApplied(ex))
            {
                // The schema change this migration makes already exists (for
                // example EF Core Identity created the column first). Treat it
                // as applied instead of crashing startup.
                _logger.LogWarning(
                    ex,
                    "Migration {Version} appears to be already applied; recording it as applied.",
                    version);
            }

            await connection.ExecuteAsync(
                "INSERT INTO SchemaVersions (Version, AppliedUtc) VALUES (@Version, @AppliedUtc)",
                new { Version = version, AppliedUtc = DateTime.UtcNow.ToString("o") },
                transaction: tx);
            tx.Commit();
        }
    }

    private static bool IsAlreadyApplied(Exception ex)
    {
        // SQLite reports "duplicate column name: X" when an ADD COLUMN is
        // re-run. Match on the message so we do not depend on a specific
        // provider exception type.
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
