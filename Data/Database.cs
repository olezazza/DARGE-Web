using Npgsql;

namespace Darge.Web.Data;

public static class Database
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("DARGE_DB_CONNECTION")
        ?? throw new Exception(
            "DARGE_DB_CONNECTION environment variable was not found."
        );

    public static readonly NpgsqlDataSource DataSource =
        NpgsqlDataSource.Create(ConnectionString);
}