using System.Text;
using Microsoft.Extensions.Options;
using Priston.Application.Home;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Lojas dos NPCs nos arquivos do servidor do jogo: um .npc por NPC em <see cref="GameServerOptions.NpcFolder"/>
/// (<c>C:\ServidorPT\GameServer\NPC</c>), com <c>*Name "Loja da Sabrine"</c> e as linhas de venda
/// <c>*무기판매</c> (armas), <c>*방어구판매</c> (armaduras) e <c>*잡화판매</c> (o resto), seguidas dos códigos
/// (ex.: <c>*잡화판매 EC101 EC102 BI108</c>; "없음" é nenhum). A mesma chave pode se repetir em várias linhas.
/// Os cabeçalhos são coreanos (CP949) e os nomes em português: lidos como Windows-1252, como os outros arquivos. Só leitura.
/// </summary>
internal sealed class NpcFileCatalog(IOptions<GameServerOptions> options) : INpcShopCatalog
{
    /// <summary>"무기판매" (venda de armas) em CP949 lido como Windows-1252.</summary>
    internal const string WeaponsKey = "\u00B9\u00AB\u00B1\u00E2\u00C6\u00C7\u00B8\u00C5";

    /// <summary>"방어구판매" (venda de armaduras) em CP949 lido como Windows-1252.</summary>
    internal const string ArmorsKey = "\u00B9\u00E6\u00BE\u00EE\u00B1\u00B8\u00C6\u00C7\u00B8\u00C5";

    /// <summary>"잡화판매" (venda de diversos) em CP949 lido como Windows-1252.</summary>
    internal const string MiscKey = "\u00C0\u00E2\u00C8\u00AD\u00C6\u00C7\u00B8\u00C5";

    private static readonly Encoding Korean = CreateKorean();

    public async Task<IReadOnlyList<NpcShop>> ReadShopsAsync(CancellationToken ct)
    {
        var folder = options.Value.NpcFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return [];
        }

        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"GameServer:NpcFolder não encontrada: {folder}");
        }

        var list = new List<NpcShop>();
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(p => Path.GetExtension(p).Equals(".npc", StringComparison.OrdinalIgnoreCase));
        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 256 * 1024)
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

            if (Parse(Path.GetFileName(path), text) is { } shop)
            {
                list.Add(shop);
            }
        }

        return list.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.File, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Loja de um arquivo .npc: nome (<c>*Name</c>; senão <c>*이름</c> quando não é coreano; senão o nome do arquivo)
    /// e os códigos de cada linha de venda. null quando o NPC não vende nada.
    /// </summary>
    public static NpcShop? Parse(string fileName, string text)
    {
        string? name = null;
        string? localName = null;
        var weapons = new List<string>();
        var armors = new List<string>();
        var misc = new List<string>();
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

            var target = parts[0] switch
            {
                WeaponsKey => weapons,
                ArmorsKey => armors,
                MiscKey => misc,
                _ => null,
            };
            if (target is not null)
            {
                foreach (var token in parts.Skip(1))
                {
                    var id = token.ToUpperInvariant();
                    if (ItemNames.ParseFileId(id) is not null && !target.Contains(id))
                    {
                        target.Add(id);
                    }
                }
            }
            else if (name is null && parts[0].Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                name = ItemFileCatalog.Quoted(line);
            }
            else if (localName is null && parts[0] == MonsterFileCatalog.NameKey)
            {
                localName = ItemFileCatalog.Quoted(line);
            }
        }

        if (weapons.Count + armors.Count + misc.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(localName) && !IsKorean(localName))
        {
            name = localName;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(fileName);
            while (Path.GetExtension(name).Equals(".npc", StringComparison.OrdinalIgnoreCase))
            {
                name = Path.GetFileNameWithoutExtension(name);
            }
        }

        return new NpcShop(string.IsNullOrWhiteSpace(name) ? fileName : name.Trim(), fileName, weapons, armors, misc);
    }

    /// <summary>
    /// Texto lido como Windows-1252 que, nos bytes originais, é coreano (CP949): "´ëÀå..." → "대장...".
    /// Nome em português sempre tem letra sem acento, coreano não ("Ação" em CP949 também vira hangul).
    /// </summary>
    internal static bool IsKorean(string text)
    {
        if (text.Any(char.IsAsciiLetter))
        {
            return false;
        }

        var decoded = Korean.GetString(ItemFileCatalog.FileEncoding.GetBytes(text));
        return decoded.Any(c => c is >= '\uAC00' and <= '\uD7A3');
    }

    private static Encoding CreateKorean()
    {
        _ = ItemFileCatalog.FileEncoding; // registra o provedor de code pages
        return Encoding.GetEncoding(949);
    }
}
