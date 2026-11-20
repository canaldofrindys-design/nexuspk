using Priston.Application.GameAdmin;
using Quartz;

namespace Priston.Worker.Jobs;

/// <summary>A cada 5 minutos libera as contas cujo banimento com prazo terminou (BlockChk volta a 0).</summary>
[DisallowConcurrentExecution]
public sealed partial class BanExpiryJob(IServiceScopeFactory scopes, ILogger<BanExpiryJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<GameAccountAdminService>();
            var lifted = await accounts.LiftExpiredAsync(context.CancellationToken).ConfigureAwait(false);
            if (lifted > 0)
            {
                LogLifted(logger, lifted);
            }
        }
#pragma warning disable CA1031 // Falha registrada; o próximo disparo tenta de novo.
        catch (Exception ex) when (!context.CancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Banimentos vencidos liberados: {Count}")]
    private static partial void LogLifted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao liberar banimentos vencidos")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
