using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Priston.Infrastructure.Persistence;

public interface IPortalDbConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
}

internal sealed class PortalDbConnectionFactory(IOptions<PortalDatabaseOptions> options) : IPortalDbConnectionFactory
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PortalDatabase:ConnectionString não está configurada.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString) { ApplicationName = "PristonPortal" };
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
