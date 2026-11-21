using Microsoft.Extensions.Options;
using Priston.Application.Home;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Drops dos monstros nos arquivos do servidor do jogo: um .inf por monstro em <see cref="GameServerOptions.MonstersFolder"/>
/// (<c>C:\ServidorPT\GameServer\Monster</c>), com <c>*Name "Elite Zumbi"</c> e linhas <c>*아이템 chance códigos...</c>
/// (ex.: <c>*아이템 3 WA161 OS106</c>; <c>*아이템 50 돈 2000 5000</c> é ouro e não conta). Os cabeçalhos são coreanos (CP949):
/// lidos como Windows-1252, a chave "아이템" aparece como "¾ÆÀÌÅÛ". Só leitura; o cache fica em <see cref="ItemNameSource"/>.
/// </summary>
internal sealed class MonsterFileCatalog(IOptions<GameServerOptions> options) : IMonsterDropCatalog
{
    /// <summary>"아이템" (item) em CP949 lido como Windows-1252.</summary>
    internal const string ItemKey = "\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB";

    /// <summary>"이름" (nome) em CP949 lido como Windows-1252.</summary>
    internal const string NameKey = "\u00C0\u00CC\u00B8\u00A7";

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct)
    {
        var folder = options.Value.MonstersFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }

        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"GameServer:MonstersFolder não encontrada: {folder}");
        }

        var sources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
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

            if (Parse(Path.GetFileNameWithoutExtension(path), text) is not { } monster)
            {
                continue;
            }

            foreach (var id in monster.Drops)
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

    /// <summary>
    /// Nome do monstro e códigos dos itens que ele derruba. O nome vem de <c>*Name</c> (ou <c>*이름</c>); sem ele,
    /// do nome do arquivo depois do mapa ("Slab_119_Elite Zumbi" → "Elite Zumbi"). null sem nome ou sem item.
    /// </summary>
    public static (string Name, IReadOnlyList<string> Drops)? Parse(string fileStem, string text)
    {
        string? name = null;
        var drops = new List<string>();
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
            else if (parts[0] == ItemKey)
            {
                foreach (var token in parts.Skip(1))
                {
                    if (ItemNames.ParseFileId(token) is not null && !drops.Contains(token, StringComparer.OrdinalIgnoreCase))
                    {
                        drops.Add(token.ToUpperInvariant());
                    }
                }
            }
        }

        name = string.IsNullOrWhiteSpace(name) ? FromFileName(fileStem) : name.Trim();
        return name is null || drops.Count == 0 ? null : (name, drops);
    }

    private static string? FromFileName(string stem)
    {
        var parts = stem.Split('_', 3);
        var name = parts.Length == 3 && parts[1].All(char.IsAsciiDigit) ? parts[2] : stem;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
