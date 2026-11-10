using Priston.Application.Characters;
using Quartz;

namespace Priston.Worker.Jobs;

/// <summary>Foto diária de nível. Roda logo após a meia-noite de Brasília e ao iniciar o serviço (se faltar a de hoje).</summary>
[DisallowConcurrentExecution]
public sealed partial class LevelSnapshotJob(LevelSnapshotService snapshots, ILogger<LevelSnapshotJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await snapshots.TakeIfMissingAsync(context.CancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Falha registrada; o próximo disparo tenta de novo.
        catch (Exception ex) when (!context.CancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao tirar a foto diária de nível")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
