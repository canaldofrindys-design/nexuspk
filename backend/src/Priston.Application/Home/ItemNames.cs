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

    /// <summary>Atributos do item no arquivo dele (<c>OpenItem\CA182.txt</c>). null quando não existe.</summary>
    Task<ItemDetail?> ReadDetailAsync(string fileId, CancellationToken ct) => Task.FromResult<ItemDetail?>(null);
}

/// <summary>Um atributo do arquivo do item, já com o nome em português ("Defesa", "200").</summary>
public sealed record ItemStat(string Label, string Value);

/// <summary>Item do arquivo do servidor: nome, atributos, requisitos e o bônus da classe (linhas <c>**</c>).</summary>
public sealed record ItemDetail(string Id, string Name, IReadOnlyList<ItemStat> Stats, IReadOnlyList<ItemStat> Requirements, IReadOnlyList<ItemStat> Spec);

/// <summary>
/// Quais monstros podem derrubar cada item, pelos arquivos de monstro do servidor (<c>GameServer\Monster\*.inf</c>,
/// linhas <c>*아이템 chance códigos...</c>): código do arquivo do item ("WA161") → nomes dos monstros. Pasta fora do alcance: lança.
/// </summary>
public interface IMonsterDropCatalog
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct);

    /// <summary>Todos os monstros com drop, com a vida e cada linha de drop (para a consulta no painel).</summary>
    Task<IReadOnlyList<MonsterInfo>> ReadMonstersAsync(CancellationToken ct);
}

/// <summary>Uma linha de drop do monstro: chance como está no arquivo, códigos dos itens e o ouro ("2000–5000"), se houver.</summary>
public sealed record MonsterDropGroup(string Chance, IReadOnlyList<string> Items, string? Gold);

/// <summary>Monstro do arquivo do servidor: nome, arquivo, vida e linhas de drop.</summary>
public sealed record MonsterInfo(string Name, string File, long? Hp, IReadOnlyList<MonsterDropGroup> Drops);

/// <summary>Loja de um NPC do servidor do jogo: nome, arquivo e os códigos dos itens à venda (armas, armaduras e o resto).</summary>
public sealed record NpcShop(string Name, string File, IReadOnlyList<string> Weapons, IReadOnlyList<string> Armors, IReadOnlyList<string> Misc);

/// <summary>
/// Lojas dos NPCs nos arquivos do servidor do jogo (<c>GameServer\NPC\*.npc</c>, linhas <c>*무기판매</c>, <c>*방어구판매</c>
/// e <c>*잡화판매</c> com os códigos). Só os NPCs que vendem algo. Pasta fora do alcance: lança.
/// </summary>
public interface INpcShopCatalog
{
    Task<IReadOnlyList<NpcShop>> ReadShopsAsync(CancellationToken ct);
}

/// <summary>Imagem de um item do cliente do jogo (<c>it&lt;código&gt;.bmp</c>).</summary>
public sealed record ItemImage(byte[] Data, string ContentType);

/// <summary>
/// Imagens dos itens copiadas do cliente do jogo (<c>image\Sinimage\Items</c>), pelo código do arquivo ("WA161").
/// Pasta fora do alcance: lança.
/// </summary>
public interface IItemImageStore
{
    /// <summary>Códigos (em maiúsculas) que têm imagem.</summary>
    Task<IReadOnlySet<string>> ListIdsAsync(CancellationToken ct);

    /// <summary>null quando o item não tem imagem.</summary>
    Task<ItemImage?> GetAsync(string fileId, CancellationToken ct);
}

/// <summary>
/// Nome de cada ItemCode: o nome exato do arquivo do servidor quando existe; senão o tipo com o número ("Machado nº 61");
/// senão o grupo e o código.
/// </summary>
public sealed partial class ItemNames(
    IReadOnlyDictionary<int, string> typeNames,
    IReadOnlyDictionary<int, string> exact,
    IReadOnlyDictionary<int, IReadOnlyList<string>>? sources = null,
    IReadOnlyDictionary<string, string>? fileNames = null,
    IReadOnlyDictionary<int, string>? filePrefixes = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? fileSources = null)
{
    /// <summary>Acima de tantos monstros o item é "de qualquer lugar" e o drop fica sem boss.</summary>
    public const int MaxSources = 2;

    public static readonly ItemNames Empty = new(new Dictionary<int, string>(), new Dictionary<int, string>());

    public int ExactCount => exact.Count;

    public int SourceCount => sources?.Count ?? 0;

    /// <summary>
    /// Código do arquivo do item ("CA182") a partir do ItemCode, pelo prefixo do tipo; null quando o tipo não tem prefixo certo.
    /// </summary>
    public string? FileId(int code) =>
        filePrefixes is not null && filePrefixes.TryGetValue(GameItemCode.TypeKey(code), out var prefix) && GameItemCode.Number(code) > 0
            ? prefix + GameItemCode.Number(code).ToString(GameItemCode.Number(code) < 100 ? "D2" : "D3", System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private Dictionary<string, List<string>>? byName;

    /// <summary>
    /// Código do arquivo pelo ItemCode e, quando o tipo não tem prefixo conhecido, pelo nome do item (o nome gravado no
    /// personagem é o do arquivo do item). Com mais de um arquivo de mesmo nome, vale o de mesmo número do ItemCode.
    /// </summary>
    public string? FileId(int code, string? name)
    {
        if (FileId(code) is { } id)
        {
            return id;
        }

        var key = name?.Trim();
        if (string.IsNullOrEmpty(key) || FileNames.Count == 0)
        {
            return null;
        }

        byName ??= FileNames
            .GroupBy(f => f.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Key).Order(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);
        if (!byName.TryGetValue(key, out var ids))
        {
            return null;
        }

        if (ids.Count == 1)
        {
            return ids[0];
        }

        var number = GameItemCode.Number(code);
        var same = ids.Where(i => ParseFileId(i)?.Number == number).ToList();
        return same.Count == 1 ? same[0] : null;
    }

    /// <summary>
    /// Monstros que derrubam cada item, pelo código do arquivo em maiúsculas ("CA182" → ["Valento"]).
    /// Não depende do banco do jogo (os arquivos de monstro já usam o código do arquivo).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FileSources { get; } = fileSources ?? new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Nomes pelo código do arquivo do item, em maiúsculas ("CA182" → "Traje Caos F").</summary>
    public IReadOnlyDictionary<string, string> FileNames { get; } = fileNames ?? new Dictionary<string, string>();

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

        var byId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, name) in fileNames)
        {
            if (!string.IsNullOrWhiteSpace(name) && ParseFileId(id) is not null)
            {
                byId.TryAdd(id.Trim().ToUpperInvariant(), name.Trim());
            }
        }

        var bySourceId = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, monsters) in dropSources ?? new Dictionary<string, IReadOnlyList<string>>())
        {
            if (ParseFileId(id) is null)
            {
                continue;
            }

            var key = id.Trim().ToUpperInvariant();
            var list = monsters.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim())
                .Concat(bySourceId.GetValueOrDefault(key) ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count > 0)
            {
                bySourceId[key] = list;
            }
        }

        var reverse = prefixes.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key.ToUpperInvariant()).Order(StringComparer.Ordinal).First());
        return new ItemNames(typeNames, exact, sources, byId, reverse, bySourceId);
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
