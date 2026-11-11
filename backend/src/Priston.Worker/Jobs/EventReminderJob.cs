using Priston.Application.Content;
using Quartz;

namespace Priston.Worker.Jobs;

/// <summary>A cada 5 minutos envia os lembretes dos eventos que começam na próxima hora.</summary>
[DisallowConcurrentExecution]
public sealed partial class EventReminderJob(EventReminderService reminders, ILogger<EventReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await reminders.SendDueAsync(context.CancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Falha registrada; o próximo disparo tenta de novo.
        catch (Exception ex) when (!context.CancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar lembretes de evento")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
