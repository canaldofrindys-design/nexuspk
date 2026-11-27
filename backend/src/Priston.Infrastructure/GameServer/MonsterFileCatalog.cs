using Microsoft.Extensions.Options;
using Priston.Application.Home;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Drops dos monstros nos arquivos do servidor do jogo: um .inf por monstro em <see cref="GameServerOptions.MonstersFolder"/>
/// (<c>C:\ServidorPT\GameServer\Monster</c>), com <c>*Name "Elite Zumbi"</c> e linhas <c>*아이템 chance códigos...</c>
/// (ex.: <c>*아이템 3 WA161 OS106</c>; <c>*아이템 50 돈 2000 5000</c> é ouro). Os cabeçalhos são coreanos (CP949):
/// lidos como Windows-1252, a chave "아이템" aparece como "¾ÆÀÌÅÛ". Só leitura; o cache fica em <see cref="ItemNameSource"/>
/// e no endpoint do painel.
/// </summary>
internal sealed class MonsterFileCatalog(IOptions<GameServerOptions> options) : IMonsterDropCatalog
{
    /// <summary>"아이템" (item) em CP949 lido como Windows-1252.</summary>
    internal const string ItemKey = "\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB";

    /// <summary>"이름" (nome) em CP949 lido como Windows-1252.</summary>
    internal const string NameKey = "\u00C0\u00CC\u00B8\u00A7";

    /// <summary>"생명력" (vida) em CP949 lido como Windows-1252.</summary>
    internal const string HpKey = "\u00BB\u00FD\u00B8\u00ED\u00B7\u00C2";

    /// <summary>"레벨" (nível) em CP949 lido como Windows-1252.</summary>
    internal const string LevelKey = "\u00B7\u00B9\u00BA\u00A7";

    /// <summary>"경험치" (experiência) em CP949 lido como Windows-1252.</summary>
    internal const string ExpKey = "\u00B0\u00E6\u00C7\u00E8\u00C4\u00A1";

    /// <summary>"돈" (dinheiro) em CP949 lido como Windows-1252.</summary>
    internal const string GoldKey = "\u00B5\u00B7";

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct)
    {
        var sources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var monster in await ReadMonstersAsync(ct).ConfigureAwait(false))
        {
            foreach (var id in monster.Drops.SelectMany(d => d.Items).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!sources.TryGetValue(id, out var list))
                {
                    sources[id] = list = [];
                }

                if (!list.Contains(monster.Name, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(monster.Name);
                }
            }
        }

        return sources.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<MonsterInfo>> ReadMonstersAsync(CancellationToken ct)
    {
        var folder = options.Value.MonstersFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return [];
        }

        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"GameServer:MonstersFolder não encontrada: {folder}");
        }

        var list = new List<MonsterInfo>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.inf", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 512 * 1024)
                {
                    continue;
                }

                using var reader = new StreamReader(stream, ItemFileCatalog.FileEncoding, detectEncodingFromByteOrderMarks: false);
                text = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }

            if (ParseDetails(Path.GetFileName(path), text) is { } monster)
            {
                list.Add(monster);
            }
        }

        return list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Nome do monstro e códigos dos itens que ele derruba (sem o ouro). null sem nome ou sem item.
    /// </summary>
    public static (string Name, IReadOnlyList<string> Drops)? Parse(string fileStem, string text)
    {
        var m = ParseDetails(fileStem, text);
        var ids = m?.Drops.SelectMany(d => d.Items).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        return m is null || ids.Count == 0 ? null : (m.Name, ids);
    }

    /// <summary>
    /// Monstro completo: nome (<c>*Name</c> ou <c>*이름</c>; sem ele, o nome do arquivo depois do mapa, "Slab_119_Elite Zumbi" → "Elite Zumbi"),
    /// vida, nível (<c>*레벨</c>) e EXP (<c>*경험치</c>) quando o arquivo traz, e cada linha de drop com a chance, os itens e o ouro. null sem nome ou sem nenhuma linha de drop.
    /// </summary>
    public static MonsterInfo? ParseDetails(string fileName, string text)
    {
        string? name = null;
        long? hp = null;
        int? level = null;
        long? exp = null;
        var drops = new List<MonsterDropGroup>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('*'))
            {
                continue;
            }

            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
            {
                line = line[..comment];
            }

            var parts = line[1..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            if (name is null && (parts[0].Equals("Name", StringComparison.OrdinalIgnoreCase) || parts[0] == NameKey))
            {
                name = ItemFileCatalog.Quoted(line);
            }
            else if (hp is null && parts[0] == HpKey && parts.Length > 1 && long.TryParse(parts[1], out var h))
            {
                hp = h;
            }
            else if (level is null && (parts[0] == LevelKey || parts[0].Equals("Level", StringComparison.OrdinalIgnoreCase)) && parts.Length > 1 && int.TryParse(parts[1], out var lv) && lv > 0)
            {
                level = lv;
            }
            else if (exp is null && (parts[0] == ExpKey || parts[0].Equals("Exp", StringComparison.OrdinalIgnoreCase)) && parts.Length > 1 && long.TryParse(parts[1], out var xp) && xp >= 0)
            {
                exp = xp;
            }
            else if (parts[0] == ItemKey && parts.Length > 1)
            {
                var items = new List<string>();
                string? gold = null;
                var rest = parts.Skip(2).ToList();
                for (var i = 0; i < rest.Count; i++)
                {
                    if (rest[i] == GoldKey)
                    {
                        var nums = rest.Skip(i + 1).TakeWhile(t => long.TryParse(t, out _)).ToList();
                        gold = nums.Count switch { 0 => "", 1 => nums[0], _ => $"{nums[0]}–{nums[1]}" };
                        i += nums.Count;
                    }
                    else if (ItemNames.ParseFileId(rest[i]) is not null && !items.Contains(rest[i], StringComparer.OrdinalIgnoreCase))
                    {
                        items.Add(rest[i].ToUpperInvariant());
                    }
                }

                if (items.Count > 0 || gold is not null)
                {
                    drops.Add(new MonsterDropGroup(parts[1], items, gold));
                }
            }
        }

        name = string.IsNullOrWhiteSpace(name) ? FromFileName(Path.GetFileNameWithoutExtension(fileName)) : name.Trim();
        return name is null || drops.Count == 0 ? null : new MonsterInfo(name, fileName, hp, drops, level, exp);
    }

    private static string? FromFileName(string stem)
    {
        var parts = stem.Split('_', 3);
        var name = parts.Length == 3 && parts[1].All(char.IsAsciiDigit) ? parts[2] : stem;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
