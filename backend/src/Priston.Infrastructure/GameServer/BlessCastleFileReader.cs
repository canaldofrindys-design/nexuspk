using System.Buffers.Binary;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Lê o dono do Bless Castle do arquivo <c>BlessCastle.dat</c> do servidor do jogo, como o site antigo fazia
/// (<c>src/include/rankings.service.php</c>): 8 bytes, e o número do ícone do clã dono é o inteiro de 32 bits
/// (little-endian) a partir do byte 4. O valor 1000000000 (ou 0) quer dizer castelo sem dono.
/// O arquivo é lido no máximo uma vez por minuto.
/// </summary>
internal sealed class BlessCastleFileReader(IOptions<GameServerOptions> options, IMemoryCache cache) : IBlessCastleOwnerReader
{
    public const int NoOwner = 1_000_000_000;
    private const string CacheKey = "game:bless-castle-owner";

    public async Task<int?> ReadOwnerIconIdAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out int? cached))
        {
            return cached;
        }

        var path = options.Value.BlessCastleFile;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("GameServer:BlessCastleFile não configurado.");
        }

        // O servidor do jogo pode estar gravando o arquivo: abre com compartilhamento total.
        var buffer = new byte[8];
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            await stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        }

        var owner = Parse(buffer);
        cache.Set(CacheKey, owner, TimeSpan.FromMinutes(1));
        return owner;
    }

    /// <summary>Número do ícone do clã dono, ou null sem dono. Lança exceção se o conteúdo não tem 8 bytes.</summary>
    public static int? Parse(ReadOnlySpan<byte> content)
    {
        if (content.Length < 8)
        {
            throw new InvalidDataException("BlessCastle.dat inválido.");
        }

        var icon = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(4, 4));
        return icon is 0 or NoOwner or > int.MaxValue ? null : (int)icon;
    }
}
