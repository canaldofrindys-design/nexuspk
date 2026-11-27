using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace Priston.Application.Home;

/// <summary>Monstro na lista pública: nível, vida, quantos itens diferentes derruba e se derruba ouro.</summary>
public sealed record MonsterSummary(string Slug, string Name, int? Level, long? Hp, int Items, bool Gold);

/// <summary>
/// Lista pública de monstros. <paramref name="Available"/> false quando a pasta de monstros do servidor não está ao alcance
/// (nada é inventado: a página avisa que a lista está indisponível).
/// </summary>
public sealed record MonsterList(bool Available, int Total, IReadOnlyList<MonsterSummary> Monsters);

/// <summary>Item de uma linha de drop. <paramref name="PerKill"/>: chance da linha dividida pelos itens que ela sorteia (texto, em %).</summary>
public sealed record MonsterDropItem(string Id, string Name, bool Image, string? PerKill);

/// <summary>Linha de drop como está no arquivo: chance, entre quantos itens sorteia, ouro (faixa) e os itens.</summary>
public sealed record MonsterDropLine(string Chance, int Choices, string? Gold, IReadOnlyList<MonsterDropItem> Items);

/// <summary>Página pública de um monstro, só com o que está no arquivo <c>.inf</c> do servidor.</summary>
public sealed record MonsterPage(string Slug, string Name, int? Level, long? Hp, long? Exp, IReadOnlyList<MonsterDropLine> Drops);

/// <summary>
/// Banco de monstros público: lê os mesmos arquivos <c>GameServer\Monster\*.inf</c> do painel (só leitura) e junta o nome
/// e a foto de cada item. O endereço de cada monstro é o nome do arquivo sem a extensão; nunca abre arquivo pelo que vem da URL,
/// só procura na lista já lida. Lista em cache por 10 minutos (1 minuto quando a pasta falha).
/// </summary>
public sealed class MonsterGuide(IMemoryCache cache, IMonsterDropCatalog? monsters = null, ItemNameSource? names = null, IItemImageStore? images = null)
{
    public const int MaxQueryLength = 40;
    private const string CacheKey = "monster-guide:list";

    public async Task<MonsterList> ListAsync(string? query, CancellationToken ct)
    {
        var all = await ReadAsync(ct).ConfigureAwait(false);
        if (all is null)
        {
            return new MonsterList(false, 0, []);
        }

        var q = query?.Trim() ?? string.Empty;
        IEnumerable<MonsterInfo> hits = all;
        if (q.Length > 0)
        {
            var itemNames = names is null ? ItemNames.Empty : await names.GetAsync(ct).ConfigureAwait(false);
            hits = all.Where(m => Matches(m, q, itemNames));
        }

        var list = hits
            .Select(m => new MonsterSummary(Slug(m), m.Name, m.Level, m.Hp, m.Drops.SelectMany(d => d.Items).Distinct(StringComparer.OrdinalIgnoreCase).Count(), m.Drops.Any(d => d.Gold is not null)))
            .OrderBy(m => m.Level ?? int.MaxValue)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new MonsterList(true, all.Count, list);
    }

    /// <summary>null quando o monstro não existe (ou a pasta está fora do alcance).</summary>
    public async Task<MonsterPage?> GetAsync(string slug, CancellationToken ct)
    {
        var all = await ReadAsync(ct).ConfigureAwait(false);
        var m = all?.FirstOrDefault(x => string.Equals(Slug(x), slug, StringComparison.OrdinalIgnoreCase));
        if (m is null)
        {
            return null;
        }

        var itemNames = names is null ? ItemNames.Empty : await names.GetAsync(ct).ConfigureAwait(false);
        var withImage = await ImageIdsAsync(ct).ConfigureAwait(false);
        var lines = m.Drops
            .Select(d => new MonsterDropLine(
                d.Chance,
                d.Items.Count,
                d.Gold,
                d.Items.Select(id => new MonsterDropItem(id, itemNames.FileNames.GetValueOrDefault(id) ?? id, withImage.Contains(id), PerKill(d.Chance, d.Items.Count))).ToList()))
            .ToList();
        return new MonsterPage(Slug(m), m.Name, m.Level, m.Hp, m.Exp, lines);
    }

    /// <summary>Endereço do monstro: o nome do arquivo sem ".inf".</summary>
    public static string Slug(MonsterInfo m) => Path.GetFileNameWithoutExtension(m.File);

    /// <summary>Chance de um item numa morte: a da linha dividida pelos itens sorteados. null quando a linha tem um item só (a chance é a da linha).</summary>
    public static string? PerKill(string chance, int choices)
    {
        if (choices <= 1 || !double.TryParse(chance, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) || c <= 0)
        {
            return null;
        }

        var each = c / choices;
        return each.ToString(each >= 1 ? "0.#" : "0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Procura pelo nome do monstro, pelo código de um item que ele derruba ou pelo nome desse item.</summary>
    public static bool Matches(MonsterInfo m, string q, ItemNames itemNames) =>
        m.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
        || m.Drops.SelectMany(d => d.Items).Any(id =>
            id.Equals(q, StringComparison.OrdinalIgnoreCase)
            || (itemNames.FileNames.GetValueOrDefault(id)?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>
    /// Monstros únicos: o mesmo monstro copiado em vários arquivos (um por mapa), com o mesmo nome e os mesmos drops,
    /// aparece uma vez. null quando a pasta não pode ser lida ou está vazia.
    /// </summary>
    private async Task<IReadOnlyList<MonsterInfo>?> ReadAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<MonsterInfo>? hit) && hit is not null)
        {
            return hit.Count == 0 ? null : hit;
        }

        IReadOnlyList<MonsterInfo> list = [];
        if (monsters is not null)
        {
            try
            {
                list = Distinct(await monsters.ReadMonstersAsync(ct).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                list = [];
            }
        }

        cache.Set(CacheKey, list, list.Count == 0 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10));
        return list.Count == 0 ? null : list;
    }

    public static IReadOnlyList<MonsterInfo> Distinct(IReadOnlyList<MonsterInfo> all) =>
        all
            .GroupBy(m => m.Name.Trim().ToUpperInvariant() + "|" + string.Join(";", m.Drops.Select(d => d.Chance + ":" + d.Gold + ":" + string.Join(",", d.Items))))
            .Select(g => g.OrderBy(m => m.File, StringComparer.OrdinalIgnoreCase).First())
            .ToList();

    private async Task<IReadOnlySet<string>> ImageIdsAsync(CancellationToken ct)
    {
        if (images is null)
        {
            return new HashSet<string>();
        }

        try
        {
            return await cache.GetOrCreateAsync("monster-guide:images", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await images.ListIdsAsync(ct).ConfigureAwait(false);
            }).ConfigureAwait(false) ?? new HashSet<string>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HashSet<string>();
        }
    }
}
