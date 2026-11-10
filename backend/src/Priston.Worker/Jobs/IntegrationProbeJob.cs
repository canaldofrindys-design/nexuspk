using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;

namespace Priston.Worker.Jobs;

/// <summary>
/// Verifica periodicamente os dois bancos e registra o resultado no log.
/// As coletas de ranking e status entram nas Fases 7 e 11.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class IntegrationProbeJob(HealthCheckService health, ILogger<IntegrationProbeJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var report = await health.CheckHealthAsync(c => c.Tags.Contains("ready"), context.CancellationToken).ConfigureAwait(false);
        foreach (var (name, entry) in report.Entries)
        {
            LogProbe(logger, name, entry.Status, entry.Description);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Verificação {Check}: {Status} ({Description})")]
    private static partial void LogProbe(ILogger logger, string check, HealthStatus status, string? description);
}
