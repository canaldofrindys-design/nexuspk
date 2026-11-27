using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Home;
using Priston.Domain.Time;

namespace Priston.Application.Characters;

public enum ItemEventKind
{
    Aging,
    Craft,
}

/// <summary>Uma linha do log de itens do jogo de aging ou craft. <paramref name="AtLocal"/>: horário de Brasília, como o servidor grava.</summary>
public sealed record ItemEventRow(int ItemCode, string Character, DateTime AtLocal);

/// <summary>
/// Aging e craft no log de itens do jogo (<c>ItemLogDB.dbo.IL</c>), pelos valores de Flag configurados, sem GMs nem contas escondidas.
/// A conta e o IP nunca saem daqui.
/// </summary>
public interface IItemEventLogReader
{
    /// <summary>A equipe informou os valores de Flag desse tipo? Sem eles, o histórico fica desligado.</summary>
    bool IsConfigured(ItemEventKind kind);

    Task<IReadOnlyList<ItemEventRow>> ReadRecentAsync(ItemEventKind kind, DateTime sinceLocal, int max, CancellationToken ct);
}

/// <summary>
/// Subida de nível vista nas fotos diárias. <paramref name="Day"/>: o dia em que subiu (a foto é tirada logo depois da meia-noite,
/// então mostra o nível do fim do dia anterior). <paramref name="SinceDay"/>: quando faltam fotos entre as duas, o primeiro dia possível.
/// <paramref name="Today"/>: subida de hoje, ainda sem foto (nível atual comparado com a foto da madrugada).
/// </summary>
public sealed record LevelUpEvent(string Name, int ClassCode, string ClassName, int FromLevel, int ToLevel, DateOnly Day, DateOnly? SinceDay, bool Today);

/// <summary>Aging ou craft de um personagem, com o nome e a foto do item quando o servidor tem.</summary>
public sealed record ItemHistoryEvent(string Character, string Item, string? ItemId, DateTimeOffset AtUtc);

/// <summary>
/// Página do histórico público. <paramref name="Available"/> false: fonte fora do ar. <paramref name="Configured"/> false: o tipo ainda
/// não foi ligado pela equipe. <paramref name="Since"/>: data local mais antiga que a página cobre.
/// </summary>
public sealed record HistoryPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, bool Available, bool Configured, DateOnly? Since);

/// <summary>
/// Históricos públicos: níveis (fotos diárias do portal, sem histórico antes da primeira foto), aging e craft (log de itens do jogo,
/// só com os Flags configurados). Sempre só personagens do ranking público. Respostas em cache por 5 minutos.
/// </summary>
public sealed class PublicHistoryService(
    RankingService levels,
    ILevelSnapshotStore snapshots,
    RankingPeriods periods,
    IMemoryCache cache,
    TimeProvider clock,
    IItemEventLogReader? itemEvents = null,
    ItemNameSource? itemNames = null,
    IItemImageStore? images = null)
{
    public const int WindowDays = 30;
    public const int MaxPageSize = 100;
    private const int MaxScanned = 3000;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public async Task<HistoryPage<LevelUpEvent>> GetLevelUpsAsync(int page, int pageSize, CancellationToken ct)
    {
        var today = periods.LocalDate(clock.GetUtcNow());
        var from = today.AddDays(-WindowDays);
        List<LevelUpEvent>? all;
        try
        {
            all = await cache.GetOrCreateAsync($"history:levels:{today:yyyyMMdd}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = CacheFor;
                var points = await snapshots.ReadSinceAsync(from, ct).ConfigureAwait(false);
                var current = await levels.GetLevelRankingAsync(ct).ConfigureAwait(false);
                return BuildLevelUps(points, current, today);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HistoryPage<LevelUpEvent>([], 0, 1, pageSize, false, true, null);
        }

        return Paged(all ?? [], page, pageSize, true, true, from);
    }

    /// <summary>
    /// Compara fotos seguidas de cada personagem e, no fim, a última foto com o nível de agora (subida de hoje).
    /// Só entram personagens do ranking atual (sem GMs e escondidos), com a classe de agora. O mais novo primeiro.
    /// </summary>
    public static List<LevelUpEvent> BuildLevelUps(IReadOnlyList<LevelSnapshotPoint> points, IReadOnlyList<RankedCharacter> current, DateOnly today)
    {
        var now = current.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var events = new List<LevelUpEvent>();
        foreach (var g in points.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!now.TryGetValue(g.Key, out var c))
            {
                continue;
            }

            var series = g.GroupBy(p => p.Date).Select(d => d.MaxBy(p => p.Level)!).OrderBy(p => p.Date).ToList();
            for (var i = 1; i < series.Count; i++)
            {
                var (before, after) = (series[i - 1], series[i]);
                if (after.Level > before.Level)
                {
                    var day = after.Date.AddDays(-1);
                    events.Add(new LevelUpEvent(c.Name, c.ClassCode, c.ClassName, before.Level, after.Level, day, before.Date < day ? before.Date : null, false));
                }
            }

            var last = series[^1];
            if (c.Level > last.Level && last.Date <= today)
            {
                var since = last.Date < today ? last.Date : (DateOnly?)null;
                events.Add(new LevelUpEvent(c.Name, c.ClassCode, c.ClassName, last.Level, c.Level, today, since, true));
            }
        }

        return events
            .OrderByDescending(e => e.Day)
            .ThenByDescending(e => e.ToLevel)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<HistoryPage<ItemHistoryEvent>> GetItemEventsAsync(ItemEventKind kind, int page, int pageSize, CancellationToken ct)
    {
        var today = periods.LocalDate(clock.GetUtcNow());
        var from = today.AddDays(-WindowDays);
        if (itemEvents is null || !itemEvents.IsConfigured(kind))
        {
            return new HistoryPage<ItemHistoryEvent>([], 0, 1, pageSize, true, false, from);
        }

        List<ItemHistoryEvent>? all;
        try
        {
            all = await cache.GetOrCreateAsync($"history:{kind}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = CacheFor;
                var rows = await itemEvents.ReadRecentAsync(kind, from.ToDateTime(TimeOnly.MinValue), MaxScanned, ct).ConfigureAwait(false);
                var names = itemNames is null ? ItemNames.Empty : await itemNames.GetAsync(ct).ConfigureAwait(false);
                IReadOnlySet<string> withImage = new HashSet<string>();
                if (images is not null)
                {
                    try
                    {
                        withImage = await images.ListIdsAsync(ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        withImage = new HashSet<string>();
                    }
                }

                return rows
                    .OrderByDescending(r => r.AtLocal)
                    .Select(r => new ItemHistoryEvent(
                        r.Character,
                        names.Name(r.ItemCode),
                        names.FileId(r.ItemCode) is { } id && withImage.Contains(id) ? id : null,
                        RareDropFeed.ToUtc(r.AtLocal)))
                    .ToList();
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HistoryPage<ItemHistoryEvent>([], 0, 1, pageSize, false, true, from);
        }

        return Paged(all ?? [], page, pageSize, true, true, from);
    }

    private static HistoryPage<T> Paged<T>(List<T> all, int page, int pageSize, bool available, bool configured, DateOnly? since)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var p = Math.Max(1, page);
        return new HistoryPage<T>(all.Skip((p - 1) * size).Take(size).ToList(), all.Count, p, size, available, configured, since);
    }
}
