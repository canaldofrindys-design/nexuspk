using Microsoft.Extensions.Logging;
using Priston.Domain.Time;

namespace Priston.Application.Characters;

/// <summary>
/// Tira a foto diária de nível de todos os personagens (uma por data local de Brasília).
/// É a base dos rankings diário, semanal e mensal: só existe histórico a partir da primeira foto.
/// </summary>
public sealed partial class LevelSnapshotService(
    ILevelRankingReader reader,
    ILevelSnapshotStore store,
    RankingPeriods periods,
    TimeProvider clock,
    ILogger<LevelSnapshotService> logger)
{
    /// <summary>Fotos mais antigas que isso são apagadas (cobre o mês anterior com folga).</summary>
    public const int RetentionDays = 400;

    public async Task<bool> TakeIfMissingAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = periods.LocalDate(now);
        if (await store.ExistsAsync(today, ct))
        {
            return false;
        }

        var rows = await reader.ReadAllAsync(ct);
        await store.SaveAsync(today, now, rows, ct);
        var removed = await store.DeleteBeforeAsync(today.AddDays(-RetentionDays), ct);
        LogSnapshot(logger, today, rows.Count, removed);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Foto de nível de {Date}: {Rows} personagens ({Removed} linhas antigas removidas)")]
    private static partial void LogSnapshot(ILogger logger, DateOnly date, int rows, int removed);
}
