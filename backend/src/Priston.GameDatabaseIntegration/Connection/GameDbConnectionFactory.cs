using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Priston.GameDatabaseIntegration.Connection;

public interface IGameDbConnectionFactory
{
    /// <summary>Abre uma conexão com o login somente leitura.</summary>
    Task<SqlConnection> OpenReadAsync(CancellationToken cancellationToken);
}

internal sealed class GameDbConnectionFactory(IOptions<GameDatabaseOptions> options) : IGameDbConnectionFactory
{
    public async Task<SqlConnection> OpenReadAsync(CancellationToken cancellationToken)
    {
        var connectionString = options.Value.ReadConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("GameDatabase:ReadConnectionString não está configurada.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ApplicationName = "PristonPortal.GameRead",
        };

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
