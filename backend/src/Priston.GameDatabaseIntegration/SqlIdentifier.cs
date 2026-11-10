using System.Text.RegularExpressions;

namespace Priston.GameDatabaseIntegration;

/// <summary>
/// Monta nomes de objetos SQL a partir de configuração confiável.
/// Aceita só letras, dígitos e sublinhado, para que nenhum valor externo vire SQL.
/// </summary>
public static partial class SqlIdentifier
{
    [GeneratedRegex("^[A-Za-z0-9_]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();

    public static string Quote(string name)
    {
        if (string.IsNullOrEmpty(name) || !SafeName().IsMatch(name))
        {
            throw new ArgumentException("Nome de objeto SQL inválido.", nameof(name));
        }

        return "[" + name + "]";
    }
}
