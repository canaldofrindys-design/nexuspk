using Priston.Domain.Characters;

namespace Priston.Application.Characters;

/// <summary>
/// Quem fica fora dos rankings públicos (home, ranking por nível, PvP, SoD, busca e perfil), além das contas de
/// <c>UserDB.dbo.GameMasters</c>: contas e personagens da equipe e de teste, cadastrados em <c>PortalDB.dbo.RankingExclusion</c>.
/// Nada é apagado do jogo: o personagem só deixa de aparecer no site.
/// </summary>
public sealed class RankingExclusions
{
    public static readonly RankingExclusions None = new([], [], []);

    private readonly HashSet<string> _accounts;
    private readonly HashSet<string> _characters;
    private readonly string[] _prefixes;

    public RankingExclusions(IEnumerable<string> accounts, IEnumerable<string> characters, IEnumerable<string> namePrefixes)
    {
        _accounts = new HashSet<string>(Clean(accounts), StringComparer.OrdinalIgnoreCase);
        _characters = new HashSet<string>(Clean(characters), StringComparer.OrdinalIgnoreCase);
        _prefixes = Clean(namePrefixes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public int Count => _accounts.Count + _characters.Count + _prefixes.Length;

    /// <summary>
    /// true quando a linha não pode aparecer no ranking: conta ou personagem cadastrado, nome que começa com um prefixo
    /// cadastrado (ex.: [ADM]) ou classe fora das 11 do jogo ("Desconhecida").
    /// </summary>
    public bool Hides(string? account, string? name, int classCode)
    {
        return !CharacterClassNames.IsKnown(classCode) || HidesPerson(account, name);
    }

    /// <summary>Como <see cref="Hides"/>, sem olhar a classe (fontes que não trazem a classe, como o log de itens).</summary>
    public bool HidesPerson(string? account, string? name)
    {
        var n = name?.Trim() ?? string.Empty;
        if (n.Length == 0 || _characters.Contains(n))
        {
            return true;
        }

        var a = account?.Trim();
        if (!string.IsNullOrEmpty(a) && _accounts.Contains(a))
        {
            return true;
        }

        foreach (var p in _prefixes)
        {
            if (n.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Clean(IEnumerable<string> values) =>
        values.Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0);
}

public interface IRankingExclusionSource
{
    /// <summary>A lista atual (com cache curto). Banco do portal fora do ar: a última lista lida, ou nenhuma.</summary>
    Task<RankingExclusions> GetAsync(CancellationToken ct);
}
