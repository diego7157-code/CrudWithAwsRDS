using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace ClienteApi.Infrastructure.DbContext;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly Func<string, DbConnection> _connectionCreator;

    public DbConnectionFactory(
        string connectionString,
        Func<string, DbConnection>? connectionCreator = null)
    {
        _connectionString = connectionString;
        _connectionCreator = connectionCreator ?? (value => new SqlConnection(value));
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = _connectionCreator(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
