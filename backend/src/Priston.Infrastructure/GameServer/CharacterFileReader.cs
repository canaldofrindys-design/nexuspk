using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Options;
using Priston.Application.Player;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Arquivo de cada personagem gravado pelo servidor do jogo em <see cref="GameServerOptions.UserDataFolder"/>
/// (<c>DataServer\UserData\&lt;n&gt;\&lt;nick&gt;.dat</c>, uma subpasta numerada por personagem). Só leitura.
/// </summary>
internal sealed class CharacterFileReader(IOptions<GameServerOptions> options) : ICharacterFileReader
{
    public async Task<CharacterSheet?> ReadAsync(string characterName, CancellationToken ct)
    {
        var folder = options.Value.UserDataFolder;
        var name = characterName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(folder) || name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"GameServer:UserDataFolder não encontrada: {folder}");
        }

        var path = Find(folder, name + ".dat");
        if (path is null)
        {
            return null;
        }

        // O servidor pode estar gravando o arquivo agora: abre sem travar a escrita dele.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        var data = new byte[Math.Min(stream.Length, CharacterFile.MaxSize)];
        await stream.ReadExactlyAsync(data, ct).ConfigureAwait(false);
        var saved = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        return CharacterFile.Parse(data, name, saved);
    }

    private static string? Find(string folder, string fileName)
    {
        var direct = Path.Combine(folder, fileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        foreach (var dir in Directory.EnumerateDirectories(folder))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

/// <summary>
/// Formato do arquivo do personagem do Priston Tale (cabeçalho "GHRC"), conferido com os arquivos do servidor:
/// a ficha (smCHAR_INFO) começa no byte 0x10 com o nome; nível 0xC8; atributos 0xCC a 0xDC; combate 0xE4 a 0x108;
/// peso e resistências (short) 0x110; mana e estamina (short) 0x138 e 0x13C; experiência em 64 bits (parte baixa 0x14C,
/// alta 0x194); ouro 0x154; pontos de atributo livres 0x160; mapa 0x1E4. Os itens vêm a partir de 0x6E4: quantidade,
/// início do segundo inventário, tamanho, e cada item comprimido ([tamanho][dados]: byte com o bit alto = tantos zeros,
/// senão tantos bytes copiados). Item: posição no 12 (0 = mochila), código no 16+0x18 e nome no 16+0x1C.
/// </summary>
internal static class CharacterFile
{
    public const int MaxSize = 64 * 1024;

    private const int NameOffset = 0x10;
    private const int ItemsOffset = 0x6E4;
    private static readonly Encoding Text = CreateText();

    public static CharacterSheet? Parse(ReadOnlySpan<byte> b, string expectedName, DateTimeOffset savedUtc)
    {
        if (b.Length < ItemsOffset + 12)
        {
            return null;
        }

        var name = CString(b.Slice(NameOffset, 32));
        if (!string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var level = I(b, 0xC8);
        var experience = (long)(((ulong)U(b, 0x194) << 32) | U(b, 0x14C));
        var map = I(b, 0x1E4);
        return new CharacterSheet(
            name,
            level,
            experience,
            CharacterGameTables.ExperiencePercent(level, experience),
            I(b, 0xCC), I(b, 0xD0), I(b, 0xD4), I(b, 0xD8), I(b, 0xDC),
            Math.Max(0, I(b, 0x160)),
            I(b, 0xE4), I(b, 0xE8), I(b, 0xEC), I(b, 0xF0), I(b, 0xF8), I(b, 0xFC), I(b, 0x100), I(b, 0x104), I(b, 0x108),
            S(b, 0x138), S(b, 0x13C),
            S(b, 0x110), S(b, 0x112),
            I(b, 0x154),
            new CharacterResistances(S(b, 0x114), S(b, 0x118), S(b, 0x11A), S(b, 0x11C), S(b, 0x11E)),
            map >= 0 && map < CharacterGameTables.MapNames.Count ? map : null,
            CharacterGameTables.MapName(map),
            savedUtc,
            Equipment(b),
            I(b, 0xC4));
    }

    private static int I(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b[o..]);

    private static uint U(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);

    private static short S(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt16LittleEndian(b[o..]);

    private static List<CharacterEquipment> Equipment(ReadOnlySpan<byte> b)
    {
        var list = new List<CharacterEquipment>();
        var count = BinaryPrimitives.ReadInt32LittleEndian(b[ItemsOffset..]);
        var mainCount = BinaryPrimitives.ReadInt32LittleEndian(b[(ItemsOffset + 4)..]);
        var offset = ItemsOffset + 12;
        var buffer = new byte[1024];
        for (var i = 0; i < Math.Min(count, 400); i++)
        {
            if (offset + 4 > b.Length)
            {
                break;
            }

            var length = BinaryPrimitives.ReadInt32LittleEndian(b[offset..]);
            if (length <= 4 || offset + length > b.Length)
            {
                break;
            }

            // Só o inventário principal tem os itens vestidos; o segundo inventário (troca de equipamento) fica de fora.
            if (i < mainCount)
            {
                var size = Decompress(b.Slice(offset + 4, length - 4), buffer);
                if (size >= 16 + 0x3C)
                {
                    var item = buffer.AsSpan(0, size);
                    var slot = BinaryPrimitives.ReadInt32LittleEndian(item[12..]);
                    var code = BinaryPrimitives.ReadInt32LittleEndian(item[(16 + 0x18)..]);
                    var itemName = CString(item.Slice(16 + 0x1C, 32));
                    if (CharacterGameTables.SlotName(slot) is { } slotName && code != 0 && itemName.Length > 0 && !list.Exists(e => e.Slot == slot))
                    {
                        list.Add(new CharacterEquipment(slot, slotName, itemName, code));
                    }
                }
            }

            offset += length;
        }

        return list.OrderBy(e => e.Slot).ToList();
    }

    /// <summary>Descomprime um item: byte com o bit alto = (byte &amp; 0x7F) zeros; senão, tantos bytes copiados. Devolve o tamanho.</summary>
    internal static int Decompress(ReadOnlySpan<byte> src, byte[] dest)
    {
        var o = 0;
        var i = 0;
        while (i < src.Length && o < dest.Length)
        {
            var c = src[i++];
            var n = c & 0x7F;
            if ((c & 0x80) != 0)
            {
                n = Math.Min(n, dest.Length - o);
                dest.AsSpan(o, n).Clear();
            }
            else
            {
                n = Math.Min(Math.Min(n, src.Length - i), dest.Length - o);
                src.Slice(i, n).CopyTo(dest.AsSpan(o));
                i += n;
            }

            o += n;
        }

        return o;
    }

    private static string CString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        return Text.GetString(end < 0 ? bytes : bytes[..end]).Trim();
    }

    private static Encoding CreateText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
