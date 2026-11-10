using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Priston.Infrastructure.Persistence;

/// <summary>Confirma conexão com o banco do portal e que as migrações foram aplicadas.</summary>
internal sealed class PortalDatabaseHealthCheck(
    IPortalDbConnectionFactory connections,
    IOptions<PortalDatabaseOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = new CommandDefinition(
                "SELECT COUNT(*) FROM dbo.SchemaVersions",
                commandTimeout: options.Value.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);
            var applied = await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
            return applied > 0
                ? HealthCheckResult.Healthy("Banco do portal acessível.", new Dictionary<string, object> { ["migrations"] = applied })
                : HealthCheckResult.Degraded("Banco do portal sem migrações aplicadas.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Falha ao conectar no banco do portal.", exception: null);
        }
    }
}
