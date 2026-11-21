using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Site;

namespace Priston.Application.Home;

/// <summary>Um item pego no jogo (log de itens). <paramref name="PickedLocal"/>: horário de Brasília, como o servidor grava.</summary>
public sealed record ItemPickupRow(int ItemCode, string Character, DateTime PickedLocal);

/// <summary>Quantas vezes um código de item foi pego no período e quem pegou por último (ajuda a equipe a montar a lista de raros).</summary>
public sealed record ItemPickupStat(int ItemCode, int Times, DateTime LastLocal, string LastCharacter);

/// <summary>
/// Log de itens do jogo (<c>ItemLogDB.dbo.IL</c>, Flag 0 = pegou do chão), sem GMs nem contas escondidas do ranking.
/// A conta nunca sai daqui.
/// </summary>
public interface IItemLogReader
{
    Task<IReadOnlyList<ItemPickupRow>> ReadPickupsAsync(IReadOnlyCollection<int> itemCodes, DateTime sinceLocal, int max, CancellationToken ct);

    Task<IReadOnlyList<ItemPickupStat>> ReadTopPickupsAsync(DateTime sinceLocal, int max, CancellationToken ct);
}

/// <summary>
/// "Drops raros da semana" automáticos: cruza o log de itens com a lista de itens raros que a equipe montou no painel
/// e junta com os drops anunciados à mão. Cache de 2 minutos; log fora do ar: só os anunciados.
/// </summary>
public sealed class RareDropFeed(IMemoryCache cache, TimeProvider clock, IItemLogReader? itemLog = null)
{
    private static readonly TimeZoneInfo GameTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Priston.Domain.Time.RankingPeriods.TimeZoneId);
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

    /// <summary>Quantos drops a faixa da home mostra (os 11 espaços da prancha).</summary>
    public const int MaxShown = 11;

    public async Task<IReadOnlyList<RareDrop>> GetAsync(SiteSnapshot site, CancellationToken ct)
    {
        var manual = site.RareDrops ?? [];
        var catalog = site.RareItems ?? [];
        IReadOnlyList<RareDrop> automatic = [];
        if (itemLog is not null && catalog.Count > 0)
        {
            try
            {
                var key = "home:rare-drops:" + string.Join(',', catalog.Select(c => c.Code).Order());
                automatic = await cache.GetOrCreateAsync(key, async e =>
                {
                    e.AbsoluteExpirationRelativeToNow = CacheFor;
                    return await ReadAutomaticAsync(catalog, ct).ConfigureAwait(false);
                }).ConfigureAwait(false) ?? [];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                automatic = [];
            }
        }

        return Merge(manual, automatic);
    }

    /// <summary>Junta os anunciados e os do log (o mesmo personagem com o mesmo item no mesmo minuto conta uma vez), o mais novo primeiro.</summary>
    public static IReadOnlyList<RareDrop> Merge(IReadOnlyList<RareDrop> manual, IReadOnlyList<RareDrop> automatic) =>
        manual.Concat(automatic)
            .GroupBy(d => (d.Item.ToUpperInvariant(), d.Player.ToUpperInvariant(), d.DroppedUtc.ToUnixTimeSeconds() / 60))
            .Select(g => g.OrderByDescending(d => d.Boss is not null).First())
            .OrderByDescending(d => d.DroppedUtc)
            .Take(MaxShown)
            .ToList();

    private async Task<IReadOnlyList<RareDrop>> ReadAutomaticAsync(IReadOnlyList<RareItem> catalog, CancellationToken ct)
    {
        var byCode = catalog.GroupBy(c => c.Code).ToDictionary(g => g.Key, g => g.First());
        var since = TimeZoneInfo.ConvertTime(clock.GetUtcNow().AddDays(-SiteContentService.DropDays), GameTimeZone).DateTime;
        var rows = await itemLog!.ReadPickupsAsync(byCode.Keys, since, MaxShown * 3, ct).ConfigureAwait(false);
        return rows
            .Where(r => byCode.ContainsKey(r.ItemCode))
            .Select(r =>
            {
                var item = byCode[r.ItemCode];
                return new RareDrop(item.Name, item.Icon, null, r.Character, ToUtc(r.PickedLocal));
            })
            .ToList();
    }

    public static DateTimeOffset ToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), GameTimeZone), TimeSpan.Zero);
}
