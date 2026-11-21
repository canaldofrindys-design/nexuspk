using System.Text;
using Microsoft.Extensions.Options;
using Priston.Application.Home;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Nomes dos itens nos arquivos do servidor do jogo: um .txt por item em <see cref="GameServerOptions.ItemsFolder"/>
/// (<c>C:\ServidorPT\GameServer\OpenItem</c>), com a linha <c>*Name "Traje Caos F"</c> e o código do item ("CA182"),
/// que também é o nome do arquivo. Os arquivos têm cabeçalhos em coreano e nomes em português: lidos como Windows-1252,
/// e só o texto entre aspas é usado. Só leitura; o cache fica em <see cref="ItemNameSource"/>.
/// </summary>
internal sealed class ItemFileCatalog(IOptions<GameServerOptions> options) : IItemFileCatalog
{
    internal static readonly Encoding FileEncoding = CreateEncoding();

    public async Task<IReadOnlyDictionary<string, string>> ReadNamesAsync(CancellationToken ct)
    {
        var folder = options.Value.ItemsFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new Dictionary<string, string>();
        }

        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"GameServer:ItemsFolder não encontrada: {folder}");
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(folder, "*.txt", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try
            {
                // O servidor do jogo pode estar com o arquivo aberto: compartilhamento total.
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 256 * 1024)
                {
                    continue;
                }

                using var reader = new StreamReader(stream, FileEncoding, detectEncodingFromByteOrderMarks: false);
                text = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }

            if (Parse(Path.GetFileNameWithoutExtension(path), text) is { } item)
            {
                names.TryAdd(item.Id, item.Name);
            }
        }

        return names;
    }

    /// <summary>
    /// Código e nome de um arquivo de item. O código é o nome do arquivo quando ele é um código ("CA182");
    /// senão, o primeiro texto entre aspas que tem cara de código. null sem nome ou sem código.
    /// </summary>
    public static (string Id, string Name)? Parse(string fileStem, string text)
    {
        string? name = null;
        string? quotedId = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('*') || Quoted(line) is not { } value)
            {
                continue;
            }

            var key = line[1..].Split([' ', '\t', '"'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (name is null && key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                name = value;
            }
            else if (quotedId is null && ItemNames.ParseFileId(value) is not null)
            {
                quotedId = value;
            }
        }

        var id = ItemNames.ParseFileId(fileStem) is not null ? fileStem : quotedId;
        return string.IsNullOrWhiteSpace(name) || id is null ? null : (id.Trim().ToUpperInvariant(), name.Trim());
    }

    internal static string? Quoted(string line)
    {
        var start = line.IndexOf('"');
        var end = start < 0 ? -1 : line.IndexOf('"', start + 1);
        return end > start + 1 ? line[(start + 1)..end] : null;
    }

    private static Encoding CreateEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
