namespace Priston.Application.Accounts;

/// <summary>
/// Regras de cadastro e sessão. Os limites de tamanho seguem o que o cliente do jogo aceita;
/// confirme com o servidor antes de mudar (o banco aceita até 32 caracteres).
/// </summary>
public sealed class AccountOptions
{
    public const string SectionName = "Accounts";

    public int LoginMinLength { get; set; } = 4;

    public int LoginMaxLength { get; set; } = 16;

    public int PasswordMinLength { get; set; } = 6;

    public int PasswordMaxLength { get; set; } = 16;

    public int SessionHours { get; set; } = 12;

    public int EmailConfirmHours { get; set; } = 48;

    public int PasswordResetMinutes { get; set; } = 30;

    /// <summary>Falhas de senha por login antes de bloquear tentativas temporariamente.</summary>
    public int MaxFailuresPerLogin { get; set; } = 5;

    /// <summary>Falhas de senha por IP antes de bloquear tentativas temporariamente.</summary>
    public int MaxFailuresPerIp { get; set; } = 20;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Endereço público do portal, usado nos links enviados por e-mail.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:3000";
}
