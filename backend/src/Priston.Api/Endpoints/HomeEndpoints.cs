using Priston.Application.Home;

namespace Priston.Api.Endpoints;

public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/home", async (HomeSnapshotCache home, HttpContext http, CancellationToken ct) =>
        {
            var snapshot = await home.GetAsync(ct);
            http.Response.Headers.CacheControl = "public, max-age=30";
            return Results.Ok(snapshot);
        });
    }
}

/// <summary>Mantém a home pronta: monta ao ligar a API e de novo a cada 25 segundos, para o site nunca esperar.</summary>
public sealed partial class HomeWarmup(HomeSnapshotCache home, ILogger<HomeWarmup> log) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(25);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        do
        {
            try
            {
                await home.RefreshAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogFailed(log, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não consegui montar a home em segundo plano")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
