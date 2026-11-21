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

    public async Task<ItemDetail?> ReadDetailAsync(string fileId, CancellationToken ct)
    {
        var folder = options.Value.ItemsFolder;
        if (string.IsNullOrWhiteSpace(folder) || ItemNames.ParseFileId(fileId) is null)
        {
            return null;
        }

        // O código só tem letras e números (ParseFileId), então não sai da pasta.
        var path = Path.Combine(folder, fileId.Trim().ToUpperInvariant() + ".txt");
        if (!File.Exists(path))
        {
            return null;
        }

        string text;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 256 * 1024)
            {
                return null;
            }

            using var reader = new StreamReader(stream, FileEncoding, detectEncodingFromByteOrderMarks: false);
            text = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }

        return ParseDetail(Path.GetFileNameWithoutExtension(path), text);
    }

    /// <summary>Atributos que aparecem na página do item: chave coreana do arquivo → nome em português.</summary>
    private static readonly (string Key, string Label)[] StatKeys =
    [
        ("공격력", "Poder de ataque"), ("사정거리", "Alcance"), ("공격속도", "Velocidade de ataque"), ("명중력", "Taxa de ataque"),
        ("크리티컬", "Crítico"), ("흡수력", "Absorção"), ("방어력", "Defesa"), ("블럭율", "Bloqueio"), ("이동속도", "Velocidade"),
        ("생체", "Orgânico"), ("불", "Fogo"), ("냉기", "Gelo"), ("번개", "Raio"), ("독", "Veneno"),
        ("생명력재생", "Regeneração de HP"), ("기력재생", "Regeneração de MP"), ("근력재생", "Regeneração de RES"),
        ("생명력추가", "HP adicional"), ("기력추가", "MP adicional"), ("근력추가", "RES adicional"),
        ("마법숙련도", "Maestria mágica"), ("보유공간", "Espaço de poções"), ("내구력", "Durabilidade"), ("무게", "Peso"),
    ];

    private static readonly (string Key, string Label)[] RequirementKeys =
    [
        ("레벨", "Nível"), ("힘", "Força"), ("정신력", "Espírito"), ("재능", "Talento"), ("민첩성", "Agilidade"), ("건강", "Vitalidade"),
    ];

    /// <summary>
    /// Atributos de um arquivo de item: cada linha <c>*chave valor [valor]</c> conhecida vira "Defesa: 200" ou
    /// "Poder de ataque: 10–20"; linhas sem valor ou com zero ficam de fora. Linhas <c>**chave</c> são o bônus da classe.
    /// null sem nome.
    /// </summary>
    public static ItemDetail? ParseDetail(string fileStem, string text)
    {
        if (Parse(fileStem, text) is not { } item)
        {
            return null;
        }

        var stats = new List<ItemStat>();
        var requirements = new List<ItemStat>();
        var spec = new List<ItemStat>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
            {
                line = line[..comment];
            }

            var isSpec = line.StartsWith("**", StringComparison.Ordinal);
            if (!line.StartsWith('*'))
            {
                continue;
            }

            var parts = line.TrimStart('*').Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var key = Korean(parts[0]);
            var numbers = parts.Skip(1).TakeWhile(IsNumber).Take(2).ToList();
            if (numbers.Count == 0 || numbers.All(n => double.Parse(n, System.Globalization.CultureInfo.InvariantCulture) == 0))
            {
                continue;
            }

            var value = numbers.Count == 2 && numbers[0] != numbers[1] ? $"{numbers[0]}–{numbers[1]}" : numbers[0];
            if (RequirementKeys.FirstOrDefault(k => k.Key == key).Label is { } req && !isSpec)
            {
                requirements.Add(new ItemStat(req, value));
            }
            else if (StatKeys.FirstOrDefault(k => k.Key == key).Label is { } label)
            {
                var target = isSpec ? spec : stats;
                if (!target.Any(s => s.Label == label))
                {
                    target.Add(new ItemStat(label, value));
                }
            }
        }

        return new ItemDetail(item.Id, item.Name, stats, requirements, spec);
    }

    private static bool IsNumber(string token) =>
        double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

    /// <summary>Texto lido como Windows-1252 de volta ao coreano original (CP949): "¹æ¾î·Â" → "방어력".</summary>
    internal static string Korean(string text) => KoreanEncoding.GetString(FileEncoding.GetBytes(text));

    private static readonly Encoding KoreanEncoding = CreateKorean();

    private static Encoding CreateKorean()
    {
        _ = FileEncoding;
        return Encoding.GetEncoding(949);
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
