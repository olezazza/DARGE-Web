using Npgsql;

namespace Darge.Web.Data;

public static class Database
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("DARGE_DB_CONNECTION")
        ?? throw new InvalidOperationException(
            "DARGE_DB_CONNECTION environment variable is required. Please set DARGE_DB_CONNECTION to your PostgreSQL connection string."
        );

    public static readonly NpgsqlDataSource DataSource =
        NpgsqlDataSource.Create(ConnectionString);
}