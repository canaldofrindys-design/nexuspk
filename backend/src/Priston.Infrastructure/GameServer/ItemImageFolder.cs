using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Home;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Imagens dos itens em <see cref="GameServerOptions.ItemImagesFolder"/>: procura <c>it&lt;código&gt;.bmp</c> (ou .png/.jpg)
/// em todas as subpastas, do jeito que vieram do cliente (<c>image\Sinimage\Items\...</c>). O índice fica 10 minutos em cache.
/// Só serve arquivos que começam como BMP, PNG ou JPEG e têm até 1 MB.
/// </summary>
internal sealed class ItemImageFolder(IOptions<GameServerOptions> options, IMemoryCache cache) : IItemImageStore
{
    private const string CacheKey = "game:item-images";
    private const int MaxBytes = 1024 * 1024;
    private static readonly string[] Extensions = [".bmp", ".png", ".jpg", ".jpeg"];

    public async Task<IReadOnlySet<string>> ListIdsAsync(CancellationToken ct) =>
        new HashSet<string>((await IndexAsync(ct).ConfigureAwait(false)).Keys, StringComparer.OrdinalIgnoreCase);

    public async Task<ItemImage?> GetAsync(string fileId, CancellationToken ct)
    {
        if (ItemNames.ParseFileId(fileId) is null)
        {
            return null;
        }

        var index = await IndexAsync(ct).ConfigureAwait(false);
        if (!index.TryGetValue(fileId.Trim().ToUpperInvariant(), out var path))
        {
            return null;
        }

        byte[] data;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is 0 or > MaxBytes)
            {
                return null;
            }

            data = new byte[stream.Length];
            await stream.ReadExactlyAsync(data, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }

        return ContentType(data) is { } type ? new ItemImage(data, type) : null;
    }

    /// <summary>Tipo pelo começo do arquivo (não pela extensão). null quando não é BMP, PNG nem JPEG.</summary>
    public static string? ContentType(ReadOnlySpan<byte> data) => data switch
    {
        [(byte)'B', (byte)'M', ..] => "image/bmp",
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        _ => null,
    };

    /// <summary>"itWA161.bmp" → "WA161"; null quando o nome não é de imagem de item.</summary>
    public static string? IdFromFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase) || stem.Length < 3 || !stem.StartsWith("it", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = stem[2..];
        return ItemNames.ParseFileId(id) is null ? null : id.ToUpperInvariant();
    }

    private async Task<IReadOnlyDictionary<string, string>> IndexAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var folder = options.Value.ItemImagesFolder;
        IReadOnlyDictionary<string, string> index;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            index = new Dictionary<string, string>();
        }
        else
        {
            index = await Task.Run(() =>
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var walk = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8 };
                foreach (var path in Directory.EnumerateFiles(folder, "it*.*", walk))
                {
                    if (IdFromFileName(Path.GetFileName(path)) is { } id)
                    {
                        // .bmp do cliente primeiro; um .png com o mesmo código (feito à mão) não substitui.
                        map.TryAdd(id, path);
                    }
                }

                return (IReadOnlyDictionary<string, string>)map;
            }, ct).ConfigureAwait(false);
        }

        cache.Set(CacheKey, index, TimeSpan.FromMinutes(index.Count > 0 ? 10 : 2));
        return index;
    }
}
