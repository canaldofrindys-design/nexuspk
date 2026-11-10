using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Priston.Application.Accounts;

public enum AccountField
{
    Login,
    Password,
    NewPassword,
    Email,
}

public sealed record FieldError(AccountField Field, string Message);

/// <summary>Validação de login, senha e e-mail no cadastro e na troca de senha.</summary>
public static partial class AccountRules
{
    // Mesma regra de GameAccountTable: começa com letra ou número; depois letras, números, _ ou -.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();

    // Só letras e números: o cliente do jogo não aceita todos os símbolos e a coluna é varchar.
    [GeneratedRegex("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PasswordPattern();

    public static FieldError? ValidateLogin(string? login, AccountOptions o)
    {
        if (string.IsNullOrEmpty(login) || login.Length < o.LoginMinLength || login.Length > o.LoginMaxLength)
        {
            return new(AccountField.Login, $"O login deve ter de {o.LoginMinLength} a {o.LoginMaxLength} caracteres.");
        }

        return LoginPattern().IsMatch(login)
            ? null
            : new(AccountField.Login, "Use letras, números, _ ou -, começando por letra ou número.");
    }

    public static FieldError? ValidatePassword(string? password, string? login, AccountOptions o)
    {
        if (string.IsNullOrEmpty(password) || password.Length < o.PasswordMinLength || password.Length > o.PasswordMaxLength)
        {
            return new(AccountField.Password, $"A senha deve ter de {o.PasswordMinLength} a {o.PasswordMaxLength} caracteres.");
        }

        if (!PasswordPattern().IsMatch(password))
        {
            return new(AccountField.Password, "Use apenas letras e números na senha.");
        }

        if (!string.IsNullOrEmpty(login) && password.Contains(login, StringComparison.OrdinalIgnoreCase))
        {
            return new(AccountField.Password, "A senha não pode conter o login.");
        }

        return password.Distinct().Count() < 3
            ? new(AccountField.Password, "A senha é fraca demais. Misture letras e números.")
            : null;
    }

    public static FieldError? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            return new(AccountField.Email, "Informe um e-mail válido.");
        }

        return MailAddress.TryCreate(email.Trim(), out var parsed) && parsed.Address == email.Trim() && parsed.Host.Contains('.', StringComparison.Ordinal)
            ? null
            : new(AccountField.Email, "Informe um e-mail válido.");
    }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
