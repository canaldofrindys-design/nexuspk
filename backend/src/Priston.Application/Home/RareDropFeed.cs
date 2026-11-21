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

/// <summary>
/// Nomes dos tipos de item do jogo (<c>ItemDB.dbo.ItemType</c>), pela chave <see cref="GameItemCode.TypeKey"/>
/// (grupo e tipo, os dois bytes altos do ItemCode). Fonte fora do ar: lança; o feed usa o código decodificado.
/// </summary>
public interface IItemNameReader
{
    Task<IReadOnlyDictionary<int, string>> ReadTypeNamesAsync(CancellationToken ct);
}

/// <summary>
/// ItemCode do Priston Tale: byte alto = grupo (1 arma, 2 defesa, 3 acessório, 4 poção, 5 ouro, 8 diversos),
/// segundo byte = tipo, terceiro = número do item.
/// </summary>
public static class GameItemCode
{
    public static int Group(int code) => (code >> 24) & 0xFF;

    /// <summary>Grupo e tipo (dois bytes altos), ex.: 0x0101 para 0x01013D00.</summary>
    public static int TypeKey(int code) => (code >> 16) & 0xFFFF;

    /// <summary>Número do item dentro do tipo (terceiro byte).</summary>
    public static int Number(int code) => (code >> 8) & 0xFF;

    /// <summary>
    /// Lê o hexadecimal de ItemType ("0x01010000", "01010000", "0101" ou "0x0101") e devolve a chave do tipo.
    /// null quando não dá para ler.
    /// </summary>
    public static int? ParseTypeHex(string? hex)
    {
        var h = hex?.Trim() ?? string.Empty;
        if (h.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            h = h[2..];
        }

        if (h.Length is 0 or > 8 || !uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            return null;
        }

        return h.Length > 4 ? (int)((v >> 16) & 0xFFFF) : (int)v;
    }

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

    /// <summary>
    /// Nome do tipo vindo do banco do jogo com o número do item ("Machado nº 61"); sem ele, o grupo e o código ("Arma #01-3D").
    /// </summary>
    public static string Name(int code, IReadOnlyDictionary<int, string> typeNames) =>
        typeNames.TryGetValue(TypeKey(code), out var n) && !string.IsNullOrWhiteSpace(n)
            ? $"{n.Trim()} nº {Number(code)}"
            : $"{GroupName(code)} #{((code >> 16) & 0xFF):X2}-{Number(code):X2}";
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
        var names = await TypeNamesAsync(ct).ConfigureAwait(false);
        return recent
            .Where(r => byCode.ContainsKey(r.ItemCode) || IsRare(r.ItemCode, times.GetValueOrDefault(r.ItemCode, 1)))
            .Take(MaxShown)
            .Select(r => byCode.TryGetValue(r.ItemCode, out var item)
                ? new RareDrop(item.Name, item.Icon, null, r.Character, ToUtc(r.PickedLocal))
                : new RareDrop(GameItemCode.Name(r.ItemCode, names), GameItemCode.Icon(r.ItemCode), null, r.Character, ToUtc(r.PickedLocal)))
            .ToList();
    }

    /// <summary>Nomes dos tipos (cache de 1 hora). Sem a tabela ou sem permissão: nenhum, e o nome sai do código.</summary>
    private async Task<IReadOnlyDictionary<int, string>> TypeNamesAsync(CancellationToken ct)
    {
        if (itemNames is null)
        {
            return new Dictionary<int, string>();
        }

        try
        {
            return await cache.GetOrCreateAsync("home:item-type-names", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await itemNames.ReadTypeNamesAsync(ct).ConfigureAwait(false);
            }).ConfigureAwait(false) ?? new Dictionary<int, string>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<int, string>();
        }
    }

    /// <summary>Raro: fora de ouro e poções e pego no máximo <see cref="RareMaxPickups"/> vezes na janela.</summary>
    public static bool IsRare(int itemCode, int pickupsInWindow) =>
        !GameItemCode.IsCommon(itemCode) && pickupsInWindow <= RareMaxPickups;

    private static DateTime Local(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, GameTimeZone).DateTime;

    public static DateTimeOffset ToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), GameTimeZone), TimeSpan.Zero);
}
