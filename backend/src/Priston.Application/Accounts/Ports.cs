namespace Priston.Application.Accounts;

public enum GameCredentialResult
{
    Valid,
    Invalid,
    NotFound,
    Blocked,
    Deleted,
}

public sealed record GameCredentialCheck(GameCredentialResult Result, string? CanonicalLogin);

public enum GameAccountCreateResult
{
    Created,
    AlreadyExists,
}

/// <summary>Contas no banco original do jogo (accountdb). Implementada em Priston.GameDatabaseIntegration.</summary>
public interface IGameAccountStore
{
    /// <summary>False quando o login de escrita não está configurado: cadastro e troca de senha ficam desligados.</summary>
    bool WritesEnabled { get; }

    Task<bool> ExistsAsync(string login, CancellationToken ct);

    Task<GameCredentialCheck> CheckPasswordAsync(string login, string password, CancellationToken ct);

    Task<GameAccountCreateResult> CreateAsync(string login, string password, string? ipAddress, CancellationToken ct);

    Task<bool> ChangePasswordAsync(string login, string newPassword, CancellationToken ct);
}

public sealed record PlayerAccount(string GameLogin, string? Email, DateTimeOffset? EmailConfirmedUtc, DateTimeOffset CreatedUtc, DateTimeOffset? LastLoginUtc);

public interface IPlayerAccountRepository
{
    Task<PlayerAccount?> FindAsync(string gameLogin, CancellationToken ct);

    Task<bool> EmailInUseAsync(string normalizedEmail, string? exceptLogin, CancellationToken ct);

    /// <summary>Cria o registro web da conta se não existir. Com e-mail, troca o e-mail e zera a confirmação.</summary>
    Task UpsertAsync(string gameLogin, string? email, string? ipAddress, CancellationToken ct);

    Task MarkEmailConfirmedAsync(string gameLogin, DateTimeOffset at, CancellationToken ct);

    Task TouchLastLoginAsync(string gameLogin, DateTimeOffset at, CancellationToken ct);
}

public sealed record PlayerSession(string GameLogin, DateTimeOffset ExpiresUtc);

public interface IPlayerSessionStore
{
    Task CreateAsync(byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct);

    Task<PlayerSession?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    Task RevokeAllAsync(string gameLogin, DateTimeOffset now, CancellationToken ct);

    /// <summary>Encerra todas as sessões do login menos a indicada.</summary>
    Task<int> RevokeAllExceptAsync(string gameLogin, byte[] keepTokenHash, DateTimeOffset now, CancellationToken ct);
}

public static class TokenPurpose
{
    public const string EmailConfirm = "email-confirm";
    public const string PasswordReset = "password-reset";
}

public interface IOneTimeTokenStore
{
    Task CreateAsync(string purpose, byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, CancellationToken ct);

    /// <summary>Marca o token como usado e devolve o login, ou null se não existe, expirou ou já foi usado.</summary>
    Task<string?> ConsumeAsync(string purpose, byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    /// <summary>Devolve o login de um token ainda válido, sem consumi-lo.</summary>
    Task<string?> PeekAsync(string purpose, byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    /// <summary>Quantos tokens do propósito foram emitidos para o login desde <paramref name="since"/>.</summary>
    Task<int> CountSinceAsync(string purpose, string gameLogin, DateTimeOffset since, CancellationToken ct);
}

public sealed record EmailMessage(string To, string Subject, string TextBody);

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ICaptchaVerifier
{
    bool IsEnabled { get; }

    Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken ct);
}

public interface IAuditLog
{
    /// <summary>Grava uma linha de auditoria. Nunca passe senha, token ou cookie em <paramref name="details"/>.</summary>
    Task WriteAsync(string actorType, string? actorId, string action, string? targetType, string? targetId, string? ipAddress, string? details, CancellationToken ct);
}
