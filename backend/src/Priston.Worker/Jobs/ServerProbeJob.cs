using Priston.Application.Server;
using Quartz;

namespace Priston.Worker.Jobs;

/// <summary>
/// A cada minuto testa as portas do login e dos canais e grava o resultado (histórico da página de status).
/// Amostras com mais de 30 dias são apagadas uma vez por hora.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class ServerProbeJob(IServerProbe probe, IServerProbeStore history, TimeProvider clock, ILogger<ServerProbeJob> logger) : IJob
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        try
        {
            var snapshot = await probe.ProbeNowAsync(ct).ConfigureAwait(false);
            if (snapshot.Availability == ServerAvailability.Unknown)
            {
                return; // Nada configurado: não grava amostra inventada.
            }

            await history.RecordAsync(snapshot, ct).ConfigureAwait(false);
            if (clock.GetUtcNow().Minute == 0)
            {
                await history.PruneAsync(clock.GetUtcNow() - Retention, ct).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Falha registrada; o próximo disparo tenta de novo.
        catch (Exception ex) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao gravar o teste de porta do servidor")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
