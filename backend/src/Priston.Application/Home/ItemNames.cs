using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Priston.Application.Home;

/// <summary>Um tipo de item de <c>ItemDB.dbo.ItemType</c>: chave grupo+tipo (<see cref="GameItemCode.TypeKey"/>), nome e sigla (ex.: "WA", "DA1").</summary>
public sealed record ItemTypeInfo(int Key, string Name, string? Abbr);

/// <summary>Tipos de item do banco do jogo (<c>ItemDB.dbo.ItemType</c>). Fonte fora do ar: lança.</summary>
public interface IItemNameReader
{
    Task<IReadOnlyList<ItemTypeInfo>> ReadTypesAsync(CancellationToken ct);
}

/// <summary>
/// Nomes exatos dos itens nos arquivos do servidor do jogo (<c>GameServer\OpenItem\*.txt</c>), pelo código do arquivo
/// ("CA182" → "Traje Caos F"). Pasta fora do alcance: lança.
/// </summary>
public interface IItemFileCatalog
{
    Task<IReadOnlyDictionary<string, string>> ReadNamesAsync(CancellationToken ct);
}

/// <summary>
/// Quais monstros podem derrubar cada item, pelos arquivos de monstro do servidor (<c>GameServer\Monster\*.inf</c>,
/// linhas <c>*아이템 chance códigos...</c>): código do arquivo do item ("WA161") → nomes dos monstros. Pasta fora do alcance: lança.
/// </summary>
public interface IMonsterDropCatalog
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct);
}

/// <summary>
/// Nome de cada ItemCode: o nome exato do arquivo do servidor quando existe; senão o tipo com o número ("Machado nº 61");
/// senão o grupo e o código.
/// </summary>
public sealed partial class ItemNames(
    IReadOnlyDictionary<int, string> typeNames,
    IReadOnlyDictionary<int, string> exact,
    IReadOnlyDictionary<int, IReadOnlyList<string>>? sources = null)
{
    /// <summary>Acima de tantos monstros o item é "de qualquer lugar" e o drop fica sem boss.</summary>
    public const int MaxSources = 2;

    public static readonly ItemNames Empty = new(new Dictionary<int, string>(), new Dictionary<int, string>());

    public int ExactCount => exact.Count;

    public int SourceCount => sources?.Count ?? 0;

    /// <summary>
    /// De quem o item pode ter caído: o monstro, quando só um derruba o item, ou "A ou B" com dois. O log do jogo não diz
    /// quem foi morto, então com mais monstros fica null.
    /// </summary>
    public string? Boss(int code) =>
        sources is not null && sources.TryGetValue(code & unchecked((int)0xFFFFFF00), out var list) && list.Count is > 0 and <= MaxSources
            ? string.Join(" ou ", list)
            : null;

    public string Name(int code) =>
        exact.TryGetValue(code & unchecked((int)0xFFFFFF00), out var n) ? n : GameItemCode.FallbackName(code, typeNames);

    /// <summary>
    /// Monta os nomes a partir dos tipos do banco e dos arquivos do servidor.
    /// O código do arquivo é o prefixo do tipo (2 letras + 1 dígito) e o número do item: "CA182" = CA1, item 82.
    /// O prefixo vem da sigla do tipo: as 2 letras e o dígito dela (sem dígito, 1). Quando dois tipos dão o mesmo prefixo
    /// (Costume e Costume2 com a sigla "CA"; Ring e Ring2 com "OR1"), vale o dígito no fim do nome do tipo (sem dígito, 1).
    /// Prefixo que continua ambíguo fica sem nome exato, para nunca mostrar o nome errado.
    /// </summary>
    public static ItemNames Build(
        IReadOnlyList<ItemTypeInfo> types,
        IReadOnlyDictionary<string, string> fileNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? dropSources = null)
    {
        var typeNames = new Dictionary<int, string>();
        foreach (var t in types)
        {
            if (!string.IsNullOrWhiteSpace(t.Name))
            {
                typeNames.TryAdd(t.Key, t.Name.Trim());
            }
        }

        var prefixes = Prefixes(types);
        var exact = new Dictionary<int, string>();
        foreach (var (id, name) in fileNames)
        {
            if (string.IsNullOrWhiteSpace(name) || ParseFileId(id) is not { } parsed || !prefixes.TryGetValue(parsed.Prefix, out var key))
            {
                continue;
            }

            exact.TryAdd((key << 16) | (parsed.Number << 8), name.Trim());
        }

        var sources = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var (id, monsters) in dropSources ?? new Dictionary<string, IReadOnlyList<string>>())
        {
            if (Code(id, prefixes) is not { } code)
            {
                continue;
            }

            var list = monsters.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count > 0)
            {
                sources[code] = sources.TryGetValue(code, out var prev)
                    ? prev.Concat(list).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    : list;
            }
        }

        return new ItemNames(typeNames, exact, sources);
    }

    private static int? Code(string id, IReadOnlyDictionary<string, int> prefixes) =>
        ParseFileId(id) is { } p && prefixes.TryGetValue(p.Prefix, out var key) ? (key << 16) | (p.Number << 8) : null;

    /// <summary>Prefixo do arquivo ("CA1", "WS2", "OR2") → chave do tipo.</summary>
    public static IReadOnlyDictionary<string, int> Prefixes(IReadOnlyList<ItemTypeInfo> types)
    {
        var candidates = types
            .Where(t => t.Abbr is { Length: >= 2 } a && char.IsAsciiLetter(a[0]) && char.IsAsciiLetter(a[1]))
            .Select(t =>
            {
                var a = t.Abbr!.Trim().ToUpperInvariant();
                var digit = a.Length >= 3 && char.IsAsciiDigit(a[2]) ? a[2] : '1';
                return (Type: t, Letters: a[..2], Prefix: a[..2] + digit);
            })
            .ToList();

        // Mesmo prefixo para tipos diferentes: o dígito do fim do nome desempata.
        var assigned = candidates
            .GroupBy(c => c.Prefix)
            .SelectMany(g => g.Select(c => c.Type.Key).Distinct().Count() == 1
                ? g.Select(c => (Prefix: g.Key, c.Type.Key))
                : g.Select(c => (Prefix: c.Letters + NameDigit(c.Type.Name), c.Type.Key)));

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in assigned.GroupBy(x => x.Prefix, StringComparer.OrdinalIgnoreCase))
        {
            var keys = g.Select(x => x.Key).Distinct().ToList();
            if (keys.Count == 1)
            {
                result[g.Key] = keys[0];
            }
        }

        return result;
    }

    private static char NameDigit(string? name)
    {
        var n = name?.Trim() ?? string.Empty;
        return n.Length > 0 && char.IsAsciiDigit(n[^1]) ? n[^1] : '1';
    }

    /// <summary>"CA182" → ("CA1", 82); "WA1100" → ("WA1", 100). null quando não é um código de item.</summary>
    public static (string Prefix, int Number)? ParseFileId(string? id)
    {
        var m = FileIdPattern().Match(id?.Trim() ?? string.Empty);
        if (!m.Success)
        {
            return null;
        }

        var number = int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        return number is > 0 and <= 255 ? (m.Groups[1].Value.ToUpperInvariant(), number) : null;
    }

    [GeneratedRegex(@"^([A-Za-z]{2}[0-9])([0-9]{2,3})$")]
    private static partial Regex FileIdPattern();
}

/// <summary>
/// Junta tipos do banco, nomes dos itens e drops dos monstros nos arquivos do servidor, com cache de 1 hora. Cada fonte pode faltar sozinha:
/// sem os arquivos, o nome sai do tipo; sem o banco, do código.
/// </summary>
public sealed class ItemNameSource(IMemoryCache cache, IItemNameReader? types = null, IItemFileCatalog? files = null, IMonsterDropCatalog? monsters = null)
{
    private const string CacheKey = "home:item-names";

    public async Task<ItemNames> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out ItemNames? cached) && cached is not null)
        {
            return cached;
        }

        var typeList = await TryAsync(types is null ? null : types.ReadTypesAsync, (IReadOnlyList<ItemTypeInfo>)[], ct).ConfigureAwait(false);
        var fileNames = await TryAsync(files is null ? null : files.ReadNamesAsync, (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(), ct).ConfigureAwait(false);
        var drops = await TryAsync(monsters is null ? null : monsters.ReadDropSourcesAsync, (IReadOnlyDictionary<string, IReadOnlyList<string>>)new Dictionary<string, IReadOnlyList<string>>(), ct).ConfigureAwait(false);
        var names = ItemNames.Build(typeList, fileNames, drops);

        // Fonte que falhou volta a ser tentada logo; com tudo lido, 1 hora.
        var complete = typeList.Count > 0 && (files is null || fileNames.Count > 0) && (monsters is null || drops.Count > 0);
        cache.Set(CacheKey, names, complete ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(5));
        return names;
    }

    private static async Task<T> TryAsync<T>(Func<CancellationToken, Task<T>>? read, T empty, CancellationToken ct)
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
