using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Health;

/// <summary>
/// Confirma que o login de leitura conecta e enxerga as tabelas mapeadas
/// (DATABASE_MAPPING.md). Não lê nenhuma linha de dados.
/// </summary>
internal sealed class GameDatabaseHealthCheck(
    IGameDbConnectionFactory connections,
    IOptions<GameDatabaseOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        var levelTable = $"{SqlIdentifier.Quote(o.ServerDatabase)}.dbo.LevelTable";
        var accountTable = $"{SqlIdentifier.Quote(o.AccountDatabase)}.dbo.AGameUser";

        try
        {
            await using var connection = await connections.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            var command = new CommandDefinition(
                "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) AS Version, " +
                "CASE WHEN OBJECT_ID(@levelTable) IS NULL THEN 0 ELSE 1 END AS HasLevelTable, " +
                "CASE WHEN OBJECT_ID(@accountTable) IS NULL THEN 0 ELSE 1 END AS HasAccountTable",
                new { levelTable, accountTable },
                commandTimeout: o.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);
            var probe = await connection.QuerySingleAsync<Probe>(command).ConfigureAwait(false);

            var data = new Dictionary<string, object> { ["sqlVersion"] = probe.Version ?? "?" };
            if (probe.HasLevelTable == 0 || probe.HasAccountTable == 0)
            {
                return HealthCheckResult.Unhealthy("Tabelas mapeadas do jogo não encontradas ou sem permissão de leitura.", data: data);
            }

            return HealthCheckResult.Healthy("Banco do jogo acessível.", data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Não repassa a mensagem do driver: ela pode conter nome de servidor ou de login.
            return HealthCheckResult.Unhealthy("Falha ao conectar no banco do jogo.", exception: null);
        }
    }

    private sealed record Probe(string? Version, int HasLevelTable, int HasAccountTable);
}
