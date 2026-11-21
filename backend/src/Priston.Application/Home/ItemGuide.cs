using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace Priston.Application.Home;

/// <summary>Monstro que pode derrubar o item: a chance da linha de drop e entre quantos itens ela sorteia.</summary>
public sealed record ItemDropSource(string Monster, string Chance, int Choices);

/// <summary>NPC que vende o item e em qual aba ("Armas", "Armaduras", "Diversos").</summary>
public sealed record ItemShopSource(string Npc, string Tab);

/// <summary>Página pública de um item: nome, foto, atributos, de onde cai e quem vende.</summary>
public sealed record ItemGuidePage(
    string Id,
    string Name,
    bool Image,
    IReadOnlyList<ItemStat> Stats,
    IReadOnlyList<ItemStat> Requirements,
    IReadOnlyList<ItemStat> Spec,
    IReadOnlyList<ItemDropSource> Drops,
    IReadOnlyList<ItemShopSource> Shops);

/// <summary>
/// Monta a página de um item só com os arquivos do servidor do jogo (OpenItem, Monster, NPC) e as fotos do cliente.
/// Cada fonte que falha só some da página. Listas de monstros e lojas ficam 10 minutos em cache.
/// </summary>
public sealed class ItemGuide(
    IMemoryCache cache,
    ItemNameSource? names = null,
    IItemFileCatalog? files = null,
    IMonsterDropCatalog? monsters = null,
    INpcShopCatalog? shops = null,
    IItemImageStore? images = null)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>null quando o código não é de item ou o servidor não conhece o item.</summary>
    public async Task<ItemGuidePage?> GetAsync(string fileId, CancellationToken ct)
    {
        if (ItemNames.ParseFileId(fileId?.Trim() ?? string.Empty) is null)
        {
            return null;
        }

        var id = fileId!.Trim().ToUpperInvariant();
        var detail = await Try<ItemDetail?>(files is null ? null : c => files.ReadDetailAsync(id, c), null, ct).ConfigureAwait(false);
        var all = names is null ? ItemNames.Empty : await names.GetAsync(ct).ConfigureAwait(false);
        var name = detail?.Name ?? all.FileNames.GetValueOrDefault(id);
        var withImage = await Cached<IReadOnlySet<string>>("item-guide:images", images is null ? null : images.ListIdsAsync, (IReadOnlySet<string>)new HashSet<string>(), ct).ConfigureAwait(false);
        var hasImage = withImage.Contains(id);
        if (name is null && !hasImage)
        {
            return null;
        }

        var monsterList = await Cached<IReadOnlyList<MonsterInfo>>("item-guide:monsters", monsters is null ? null : monsters.ReadMonstersAsync, (IReadOnlyList<MonsterInfo>)[], ct).ConfigureAwait(false);
        var shopList = await Cached<IReadOnlyList<NpcShop>>("item-guide:shops", shops is null ? null : shops.ReadShopsAsync, (IReadOnlyList<NpcShop>)[], ct).ConfigureAwait(false);
        return new ItemGuidePage(
            id,
            name ?? id,
            hasImage,
            detail?.Stats ?? [],
            detail?.Requirements ?? [],
            detail?.Spec ?? [],
            DropsOf(id, monsterList),
            ShopsOf(id, shopList));
    }

    /// <summary>Cada linha de drop que tem o item, da maior chance para a menor (a mesma linha repetida conta uma vez).</summary>
    public static IReadOnlyList<ItemDropSource> DropsOf(string id, IReadOnlyList<MonsterInfo> monsters) =>
        monsters
            .SelectMany(m => m.Drops
                .Where(d => d.Items.Contains(id, StringComparer.OrdinalIgnoreCase))
                .Select(d => new ItemDropSource(m.Name, d.Chance, d.Items.Count)))
            .Distinct()
            .OrderByDescending(d => double.TryParse(d.Chance, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c / Math.Max(1, d.Choices) : 0)
            .ThenBy(d => d.Monster, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>NPCs que vendem o item, um por nome de NPC e aba (arquivos repetidos contam uma vez).</summary>
    public static IReadOnlyList<ItemShopSource> ShopsOf(string id, IReadOnlyList<NpcShop> shops) =>
        shops
            .SelectMany(s => new[] { ("Armas", s.Weapons), ("Armaduras", s.Armors), ("Diversos", s.Misc) }
                .Where(t => t.Item2.Contains(id, StringComparer.OrdinalIgnoreCase))
                .Select(t => new ItemShopSource(s.Name, t.Item1)))
            .Distinct()
            .OrderBy(s => s.Npc, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<T> Cached<T>(string key, Func<CancellationToken, Task<T>>? read, T empty, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out T? hit) && hit is not null)
        {
            return hit;
        }

        var value = await Try(read, empty, ct).ConfigureAwait(false);
        cache.Set(key, value, ReferenceEquals(value, empty) ? TimeSpan.FromMinutes(1) : Ttl);
        return value;
    }

    private static async Task<T> Try<T>(Func<CancellationToken, Task<T>>? read, T empty, CancellationToken ct)
    {
        if (read is null)
        {
            return empty;
        }

        try
        {
            return await read(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return empty;
        }
    }
}
