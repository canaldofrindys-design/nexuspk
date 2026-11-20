using System.Globalization;

namespace Priston.Application.GameAdmin;

public enum GameValueKind
{
    Unsupported,
    WholeNumber,
    ExactNumber,
    ApproxNumber,
    Bit,
    Text,
    DateTime,
}

/// <summary>Converte entre o texto do formulário e o tipo real da coluna. Mesmo formato na ida e na volta.</summary>
public static class GameValues
{
    public const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";

    public static GameValueKind Kind(string sqlType) => sqlType switch
    {
        "tinyint" or "smallint" or "int" or "bigint" => GameValueKind.WholeNumber,
        "decimal" or "numeric" or "money" or "smallmoney" => GameValueKind.ExactNumber,
        "float" or "real" => GameValueKind.ApproxNumber,
        "bit" => GameValueKind.Bit,
        "char" or "varchar" or "nchar" or "nvarchar" => GameValueKind.Text,
        "datetime" or "smalldatetime" or "datetime2" or "date" => GameValueKind.DateTime,
        _ => GameValueKind.Unsupported,
    };

    public static string? Format(object? value) => value switch
    {
        null or DBNull => null,
        DateTime d => d.ToString(DateFormat, CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        double f => f.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        string s => s,
        _ => value.ToString(),
    };

    /// <summary>
    /// Texto vazio vira NULL quando a coluna aceita; em texto obrigatório vira "". Números e datas obrigatórios recusam vazio.
    /// </summary>
    public static bool TryParse(GameColumn column, string? input, out object? value, out string? error)
    {
        value = null;
        error = null;
        var kind = Kind(column.SqlType);
        if (kind == GameValueKind.Unsupported)
        {
            error = "Tipo de coluna não editável pelo painel.";
            return false;
        }

        if (input is null || (kind != GameValueKind.Text && input.Trim().Length == 0) || (kind == GameValueKind.Text && input.Length == 0))
        {
            if (column.Nullable)
            {
                return true;
            }

            if (kind == GameValueKind.Text)
            {
                value = string.Empty;
                return true;
            }

            error = "Campo obrigatório.";
            return false;
        }

        var s = kind == GameValueKind.Text ? input : input.Trim();
        switch (kind)
        {
            case GameValueKind.WholeNumber:
                if (!long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
                {
                    error = "Use só números inteiros.";
                    return false;
                }

                (long min, long max) = column.SqlType switch
                {
                    "tinyint" => (0L, 255L),
                    "smallint" => (short.MinValue, (long)short.MaxValue),
                    "int" => (int.MinValue, (long)int.MaxValue),
                    _ => (long.MinValue, long.MaxValue),
                };
                if (n < min || n > max)
                {
                    error = $"Use um número entre {min} e {max}.";
                    return false;
                }

                value = column.SqlType switch { "tinyint" => (byte)n, "smallint" => (short)n, "int" => (int)n, _ => (object)n };
                return true;

            case GameValueKind.ExactNumber:
                if (!decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                {
                    error = "Use um número (ex.: 10.50).";
                    return false;
                }

                if (column.SqlType is "decimal" or "numeric" && column.Precision > 0)
                {
                    var intDigits = Math.Truncate(Math.Abs(d)).ToString(CultureInfo.InvariantCulture).TrimStart('0').Length;
                    if (intDigits > column.Precision - column.Scale)
                    {
                        error = "Número grande demais para esta coluna.";
                        return false;
                    }
                }

                value = d;
                return true;

            case GameValueKind.ApproxNumber:
                if (!double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) || double.IsNaN(f) || double.IsInfinity(f))
                {
                    error = "Use um número (ex.: 0.25).";
                    return false;
                }

                value = f;
                return true;

            case GameValueKind.Bit:
                value = s is "1" or "true" or "True" or "sim" ? true : s is "0" or "false" or "False" or "não" ? false : null;
                if (value is null)
                {
                    error = "Use 1 (sim) ou 0 (não).";
                    return false;
                }

                return true;

            case GameValueKind.DateTime:
                string[] formats = [DateFormat, "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"];
                if (!DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) || dt.Year < 1753)
                {
                    error = "Use uma data válida.";
                    return false;
                }

                value = dt;
                return true;

            default:
                if (s.Length > column.MaxChars)
                {
                    error = $"Use no máximo {column.MaxChars} caracteres.";
                    return false;
                }

                if (s.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
                {
                    error = "Tem caracteres inválidos.";
                    return false;
                }

                if (column.SqlType is "char" or "varchar" && !Latin1(s))
                {
                    error = "Esta coluna só aceita letras sem caracteres especiais (padrão do jogo).";
                    return false;
                }

                value = s;
                return true;
        }
    }

    private static bool Latin1(string s) => s.All(c => c <= 0xFF);
}
