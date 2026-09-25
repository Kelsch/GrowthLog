using System.Data;
using Microsoft.Data.Sqlite;

namespace GrowthLog.Web.Data;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // SQLite does not enforce foreign keys unless explicitly enabled per connection.
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
