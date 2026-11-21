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
    /// <summary>Todos os itens pegos desde <paramref name="sinceLocal"/>, o mais novo primeiro.</summary>
    Task<IReadOnlyList<ItemPickupRow>> ReadRecentPickupsAsync(DateTime sinceLocal, int max, CancellationToken ct);

    Task<IReadOnlyList<ItemPickupStat>> ReadTopPickupsAsync(DateTime sinceLocal, int max, CancellationToken ct);
}

/// <summary>Nomes dos itens do jogo por código (ItemCode do log). Fonte fora do ar: lança; o feed usa o código decodificado.</summary>
public interface IItemNameReader
{
    Task<IReadOnlyDictionary<int, string>> ReadNamesAsync(CancellationToken ct);
}

/// <summary>
/// ItemCode do Priston Tale: byte alto = grupo (1 arma, 2 defesa, 3 acessório, 4 poção, 5 ouro, 8 diversos),
/// segundo byte = tipo, terceiro = número do item.
/// </summary>
public static class GameItemCode
{
    public static int Group(int code) => (code >> 24) & 0xFF;

    /// <summary>Ouro e poções: nunca são "drop raro".</summary>
    public static bool IsCommon(int code) => Group(code) is 4 or 5;

    public static string Icon(int code) => Group(code) switch
    {
        1 => "martelo",
        2 => "bigorna",
        3 => "sheltom-safira",
        8 => "cristal",
        _ => "bau",
    };

    private static string GroupName(int code) => Group(code) switch
    {
        1 => "Arma",
        2 => "Equipamento",
        3 => "Acessório",
        8 => "Item especial",
        _ => "Item",
    };

    /// <summary>Nome do banco do jogo quando houver; sem ele, o grupo e o código ("Arma #01-3D").</summary>
    public static string Name(int code, IReadOnlyDictionary<int, string> names) =>
        names.TryGetValue(code, out var n) && !string.IsNullOrWhiteSpace(n)
            ? n.Trim()
            : $"{GroupName(code)} #{((code >> 16) & 0xFF):X2}-{((code >> 8) & 0xFF):X2}";
}

/// <summary>
/// "Drops raros da semana" automáticos: tudo que foi pego no log de itens e é raro (pouco pego no mês, fora ouro e poções),
/// junto com os drops anunciados à mão (opcionais). Cache de 2 minutos; log fora do ar: só os anunciados.
/// </summary>
public sealed class RareDropFeed(IMemoryCache cache, TimeProvider clock, IItemLogReader? itemLog = null, IItemNameReader? itemNames = null)
{
    private static readonly TimeZoneInfo GameTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Priston.Domain.Time.RankingPeriods.TimeZoneId);
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

    /// <summary>Quantos drops a faixa da home mostra (os 11 espaços da prancha).</summary>
    public const int MaxShown = 11;

    /// <summary>Até quantas vezes um item pode ter sido pego na janela e ainda contar como raro.</summary>
    public const int RareMaxPickups = 3;

    /// <summary>Janela (dias) usada para medir a raridade.</summary>
    public const int RarityWindowDays = 30;

    private const int MaxScanned = 5000;

    public async Task<IReadOnlyList<RareDrop>> GetAsync(SiteSnapshot site, CancellationToken ct)
    {
        var manual = site.RareDrops ?? [];
        var catalog = site.RareItems ?? [];
        IReadOnlyList<RareDrop> automatic = [];
        if (itemLog is not null)
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

    /// <summary>
    /// Raridade automática: um item é raro quando foi pego no máximo <see cref="RareMaxPickups"/> vezes nos últimos
    /// <see cref="RarityWindowDays"/> dias (ouro e poções nunca). A lista do painel, se houver, força itens como raros.
    /// </summary>
    private async Task<IReadOnlyList<RareDrop>> ReadAutomaticAsync(IReadOnlyList<RareItem> catalog, CancellationToken ct)
    {
        var byCode = catalog.GroupBy(c => c.Code).ToDictionary(g => g.Key, g => g.First());
        var now = clock.GetUtcNow();
        var since = Local(now.AddDays(-SiteContentService.DropDays));
        var recent = await itemLog!.ReadRecentPickupsAsync(since, MaxScanned, ct).ConfigureAwait(false);
        if (recent.Count == 0)
        {
            return [];
        }

        var stats = await itemLog.ReadTopPickupsAsync(Local(now.AddDays(-RarityWindowDays)), MaxScanned, ct).ConfigureAwait(false);
        var times = stats.ToDictionary(s => s.ItemCode, s => s.Times);
        var names = itemNames is null ? new Dictionary<int, string>() : await itemNames.ReadNamesAsync(ct).ConfigureAwait(false);
        return recent
            .Where(r => byCode.ContainsKey(r.ItemCode) || IsRare(r.ItemCode, times.GetValueOrDefault(r.ItemCode, 1)))
            .Take(MaxShown)
            .Select(r => byCode.TryGetValue(r.ItemCode, out var item)
                ? new RareDrop(item.Name, item.Icon, null, r.Character, ToUtc(r.PickedLocal))
                : new RareDrop(GameItemCode.Name(r.ItemCode, names), GameItemCode.Icon(r.ItemCode), null, r.Character, ToUtc(r.PickedLocal)))
            .ToList();
    }

    /// <summary>Raro: fora de ouro e poções e pego no máximo <see cref="RareMaxPickups"/> vezes na janela.</summary>
    public static bool IsRare(int itemCode, int pickupsInWindow) =>
        !GameItemCode.IsCommon(itemCode) && pickupsInWindow <= RareMaxPickups;

    private static DateTime Local(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, GameTimeZone).DateTime;

    public static DateTimeOffset ToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), GameTimeZone), TimeSpan.Zero);
}
