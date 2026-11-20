using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Admin;

/// <summary>
/// Lista de quem fica fora dos rankings (<c>PortalDB.dbo.RankingExclusion</c>, migração 0009). Lida no máximo uma vez por
/// minuto. Se o banco do portal falhar, continua valendo a última lista lida.
/// </summary>
internal sealed partial class RankingExclusionStore(
    IPortalDbConnectionFactory db,
    IOptions<PortalDatabaseOptions> options,
    TimeProvider clock,
    ILogger<RankingExclusionStore> logger) : PortalStore(db, options), IRankingExclusionSource, IDisposable
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private RankingExclusions? _current;
    private DateTimeOffset _readUtc;

    public void Dispose() => _lock.Dispose();

    public async Task<RankingExclusions> GetAsync(CancellationToken ct)
    {
        if (_current is { } fresh && clock.GetUtcNow() - _readUtc < RefreshEvery)
        {
            return fresh;
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_current is { } again && clock.GetUtcNow() - _readUtc < RefreshEvery)
            {
                return again;
            }

            try
            {
                await using var conn = await OpenAsync(ct).ConfigureAwait(false);
                var rows = (await conn.QueryAsync<(string Kind, string Value)>(Cmd(
                    "SELECT Kind, Value FROM dbo.RankingExclusion", null, ct)).ConfigureAwait(false)).ToList();
                _current = new RankingExclusions(
                    rows.Where(r => r.Kind == "conta").Select(r => r.Value),
                    rows.Where(r => r.Kind == "personagem").Select(r => r.Value),
                    rows.Where(r => r.Kind == "prefixo").Select(r => r.Value));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogReadFailed(logger, ex);
                _current ??= RankingExclusions.None;
            }

            _readUtc = clock.GetUtcNow();
            return _current;
        }
        finally
        {
            _lock.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler a lista de quem fica fora do ranking (PortalDB.dbo.RankingExclusion)")]
    private static partial void LogReadFailed(ILogger logger, Exception ex);
}
