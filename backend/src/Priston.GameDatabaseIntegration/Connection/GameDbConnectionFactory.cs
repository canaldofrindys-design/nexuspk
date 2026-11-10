using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Priston.GameDatabaseIntegration.Connection;

public interface IGameDbConnectionFactory
{
    /// <summary>Abre uma conexão com o login somente leitura.</summary>
    Task<SqlConnection> OpenReadAsync(CancellationToken cancellationToken);

    /// <summary>True quando o login de escrita (só tabelas de conta) está configurado.</summary>
    bool CanWrite { get; }

    /// <summary>Abre uma conexão com o login de escrita restrito às tabelas &lt;letra&gt;GameUser.</summary>
    Task<SqlConnection> OpenWriteAsync(CancellationToken cancellationToken);
}

internal sealed class GameDbConnectionFactory(IOptions<GameDatabaseOptions> options) : IGameDbConnectionFactory
{
    public bool CanWrite => !string.IsNullOrWhiteSpace(options.Value.WriteConnectionString);

    public Task<SqlConnection> OpenReadAsync(CancellationToken cancellationToken)
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
        return OpenAsync(builder, cancellationToken);
    }

    public Task<SqlConnection> OpenWriteAsync(CancellationToken cancellationToken)
    {
        var connectionString = options.Value.WriteConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("GameDatabase:WriteConnectionString não está configurada.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString) { ApplicationName = "PristonPortal.GameWrite" };
        return OpenAsync(builder, cancellationToken);
    }

    private static async Task<SqlConnection> OpenAsync(SqlConnectionStringBuilder builder, CancellationToken cancellationToken)
    {
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
