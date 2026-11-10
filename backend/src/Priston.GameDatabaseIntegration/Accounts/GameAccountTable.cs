using System.Text.RegularExpressions;

namespace Priston.GameDatabaseIntegration.Accounts;

/// <summary>
/// O jogo guarda as contas em uma tabela por caractere inicial do login:
/// <c>[accountdb].[dbo].[AGameUser]</c> … <c>[ZGameUser]</c> e <c>[0GameUser]</c> … <c>[9GameUser]</c>.
/// Esta classe é o único lugar que transforma um login em nome de tabela.
/// </summary>
public static partial class GameAccountTable
{
    public const int MaxLoginLength = 32;

    // userid é varchar(32); o site antigo usa a mesma regra.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();

    public static bool IsValidLogin(string? login) =>
        !string.IsNullOrEmpty(login) && LoginPattern().IsMatch(login);

    /// <summary>Retorna o sufixo do shard (A–Z ou 0–9) para um login válido.</summary>
    public static char ShardOf(string login)
    {
        if (!IsValidLogin(login))
        {
            throw new ArgumentException("Login inválido.", nameof(login));
        }

        return char.ToUpperInvariant(login[0]);
    }

    /// <summary>Nome totalmente qualificado e entre colchetes da tabela de contas do login.</summary>
    public static string QualifiedNameFor(string login, string accountDatabase)
    {
        var shard = ShardOf(login);
        return $"{SqlIdentifier.Quote(accountDatabase)}.[dbo].[{shard}GameUser]";
    }

    /// <summary>Todos os 36 shards, na ordem 0–9, A–Z.</summary>
    public static IReadOnlyList<char> AllShards { get; } =
        Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 26)).Select(c => (char)c).ToArray();
}
