using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Priston.Application.Accounts;

public enum AuthStatus
{
    Ok,
    ValidationFailed,
    CaptchaFailed,
    LoginTaken,
    EmailTaken,
    InvalidCredentials,
    Blocked,
    TooManyAttempts,
    InvalidToken,
    Unavailable,
}

public sealed record RegisterRequest(string? Login, string? Password, string? Email, string? CaptchaToken);

public sealed record LoginRequest(string? Login, string? Password, string? CaptchaToken);

public sealed record RecoveryRequest(string? Login, string? Email, string? CaptchaToken);

public sealed record ResetPasswordRequest(string? Token, string? NewPassword);

public sealed record ClientInfo(string? IpAddress, string? UserAgent);

public sealed record AuthResult(AuthStatus Status, IReadOnlyList<FieldError>? Errors = null)
{
    public static AuthResult Success { get; } = new(AuthStatus.Ok);

    public static AuthResult Of(AuthStatus status) => new(status);
}

public sealed record RegisterResult(AuthStatus Status, IReadOnlyList<FieldError>? Errors, bool ConfirmationEmailSent);

public sealed record LoginResult(AuthStatus Status, string? SessionToken, DateTimeOffset? ExpiresUtc);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record ChangeEmailRequest(string? Email);

public sealed record CurrentPlayer(string Login, string? Email, bool EmailConfirmed, DateTimeOffset SessionExpiresUtc);

/// <summary>
/// Cadastro, login, sessão e recuperação de conta.
/// A senha do jogo é guardada pelo jogo como texto (formato do accountdb); este serviço nunca a registra em log.
/// </summary>
public sealed partial class AuthService(
    IGameAccountStore gameAccounts,
    IPlayerAccountRepository players,
    IPlayerSessionStore sessions,
    IOneTimeTokenStore tokens,
    IEmailSender email,
    ICaptchaVerifier captcha,
    IAuditLog audit,
    LoginThrottle throttle,
    IOptions<AccountOptions> options,
    TimeProvider clock,
    ILogger<AuthService> logger)
{
    private const int MaxRecoveryEmailsPerHour = 3;

    private AccountOptions O => options.Value;

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, ClientInfo client, CancellationToken ct)
    {
        if (!gameAccounts.WritesEnabled)
        {
            return new(AuthStatus.Unavailable, null, false);
        }

        if (!await captcha.VerifyAsync(request.CaptchaToken, client.IpAddress, ct))
        {
            return new(AuthStatus.CaptchaFailed, null, false);
        }

        var login = request.Login?.Trim();
        var mail = request.Email?.Trim();
        var errors = new[]
        {
            AccountRules.ValidateLogin(login, O),
            AccountRules.ValidatePassword(request.Password, login, O),
            AccountRules.ValidateEmail(mail),
        }.OfType<FieldError>().ToList();
        if (errors.Count > 0)
        {
            return new(AuthStatus.ValidationFailed, errors, false);
        }

        if (await gameAccounts.ExistsAsync(login!, ct))
        {
            return new(AuthStatus.LoginTaken, null, false);
        }

        if (await players.EmailInUseAsync(AccountRules.NormalizeEmail(mail!), null, ct))
        {
            return new(AuthStatus.EmailTaken, null, false);
        }

        if (await gameAccounts.CreateAsync(login!, request.Password!, client.IpAddress, ct) == GameAccountCreateResult.AlreadyExists)
        {
            return new(AuthStatus.LoginTaken, null, false);
        }

        await players.UpsertAsync(login!, mail, client.IpAddress, ct);
        await audit.WriteAsync("player", login, "account.register", "game-account", login, client.IpAddress, null, ct);
        LogRegistered(logger, login!);

        var sent = await SendEmailConfirmationAsync(login!, mail!, ct);
        return new(AuthStatus.Ok, null, sent);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken ct)
    {
        var login = request.Login?.Trim();
        if (string.IsNullOrEmpty(login) || login.Length > 32 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 32
            || AccountRules.ValidateLogin(login, new AccountOptions { LoginMinLength = 1, LoginMaxLength = 32 }) is not null)
        {
            return new(AuthStatus.InvalidCredentials, null, null);
        }

        if (throttle.IsLocked(login, client.IpAddress))
        {
            return new(AuthStatus.TooManyAttempts, null, null);
        }

        if (!await captcha.VerifyAsync(request.CaptchaToken, client.IpAddress, ct))
        {
            return new(AuthStatus.CaptchaFailed, null, null);
        }

        var check = await gameAccounts.CheckPasswordAsync(login, request.Password, ct);
        switch (check.Result)
        {
            case GameCredentialResult.Valid:
                break;
            case GameCredentialResult.Blocked:
                await audit.WriteAsync("player", check.CanonicalLogin, "account.login.blocked", "game-account", check.CanonicalLogin, client.IpAddress, null, ct);
                return new(AuthStatus.Blocked, null, null);
            default:
                throttle.RecordFailure(login, client.IpAddress);
                return new(AuthStatus.InvalidCredentials, null, null);
        }

        var canonical = check.CanonicalLogin ?? login;
        throttle.Reset(login);
        await players.UpsertAsync(canonical, null, null, ct);

        var now = clock.GetUtcNow();
        var expires = now.AddHours(O.SessionHours);
        var (token, hash) = SecureToken.Create();
        var userAgent = client.UserAgent is { Length: > 256 } ua ? ua[..256] : client.UserAgent;
        await sessions.CreateAsync(hash, canonical, expires, client.IpAddress, userAgent, ct);
        await players.TouchLastLoginAsync(canonical, now, ct);
        await audit.WriteAsync("player", canonical, "account.login", "game-account", canonical, client.IpAddress, null, ct);
        return new(AuthStatus.Ok, token, expires);
    }

    public async Task LogoutAsync(string? sessionToken, CancellationToken ct)
    {
        if (SecureToken.LooksValid(sessionToken))
        {
            await sessions.RevokeAsync(SecureToken.Hash(sessionToken!), clock.GetUtcNow(), ct);
        }
    }

    public async Task<CurrentPlayer?> GetCurrentAsync(string? sessionToken, CancellationToken ct)
    {
        if (!SecureToken.LooksValid(sessionToken))
        {
            return null;
        }

        var session = await sessions.FindActiveAsync(SecureToken.Hash(sessionToken!), clock.GetUtcNow(), ct);
        if (session is null)
        {
            return null;
        }

        var account = await players.FindAsync(session.GameLogin, ct);
        return new(session.GameLogin, account?.Email, account?.EmailConfirmedUtc is not null, session.ExpiresUtc);
    }

    /// <summary>
    /// Sempre responde da mesma forma para não revelar se o login ou o e-mail existem.
    /// Só envia o link quando o e-mail confere com o e-mail confirmado da conta.
    /// </summary>
    public async Task<AuthResult> RequestPasswordRecoveryAsync(RecoveryRequest request, ClientInfo client, CancellationToken ct)
    {
        if (!email.IsConfigured || !gameAccounts.WritesEnabled)
        {
            return AuthResult.Of(AuthStatus.Unavailable);
        }

        if (!await captcha.VerifyAsync(request.CaptchaToken, client.IpAddress, ct))
        {
            return AuthResult.Of(AuthStatus.CaptchaFailed);
        }

        var login = request.Login?.Trim();
        var mail = request.Email?.Trim();
        if (string.IsNullOrEmpty(login) || login.Length > 32 || AccountRules.ValidateEmail(mail) is not null)
        {
            return AuthResult.Success;
        }

        var account = await players.FindAsync(login, ct);
        if (account?.Email is null || account.EmailConfirmedUtc is null
            || AccountRules.NormalizeEmail(account.Email) != AccountRules.NormalizeEmail(mail!))
        {
            await audit.WriteAsync("player", login, "account.recovery.ignored", "game-account", login, client.IpAddress, null, ct);
            return AuthResult.Success;
        }

        var now = clock.GetUtcNow();
        if (await tokens.CountSinceAsync(TokenPurpose.PasswordReset, account.GameLogin, now.AddHours(-1), ct) >= MaxRecoveryEmailsPerHour)
        {
            return AuthResult.Success;
        }

        var (token, hash) = SecureToken.Create();
        await tokens.CreateAsync(TokenPurpose.PasswordReset, hash, account.GameLogin, now.AddMinutes(O.PasswordResetMinutes), ct);
        var link = $"{O.PublicBaseUrl.TrimEnd('/')}/recuperar-conta/nova-senha?token={token}";
        await email.SendAsync(new EmailMessage(
            account.Email,
            "Redefinição de senha",
            $"""
            Recebemos um pedido para redefinir a senha da conta {account.GameLogin}.

            Para criar uma nova senha, abra o link abaixo em até {O.PasswordResetMinutes} minutos:
            {link}

            Se você não pediu, ignore este e-mail. Sua senha atual continua valendo.
            """), ct);
        await audit.WriteAsync("player", account.GameLogin, "account.recovery.sent", "game-account", account.GameLogin, client.IpAddress, null, ct);
        return AuthResult.Success;
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, ClientInfo client, CancellationToken ct)
    {
        if (!gameAccounts.WritesEnabled)
        {
            return AuthResult.Of(AuthStatus.Unavailable);
        }

        if (!SecureToken.LooksValid(request.Token))
        {
            return AuthResult.Of(AuthStatus.InvalidToken);
        }

        var hash = SecureToken.Hash(request.Token!);
        var now = clock.GetUtcNow();
        var login = await tokens.PeekAsync(TokenPurpose.PasswordReset, hash, now, ct);
        if (login is null)
        {
            return AuthResult.Of(AuthStatus.InvalidToken);
        }

        if (AccountRules.ValidatePassword(request.NewPassword, login, O) is { } error)
        {
            return new(AuthStatus.ValidationFailed, [error]);
        }

        if (await tokens.ConsumeAsync(TokenPurpose.PasswordReset, hash, now, ct) is null
            || !await gameAccounts.ChangePasswordAsync(login, request.NewPassword!, ct))
        {
            return AuthResult.Of(AuthStatus.InvalidToken);
        }

        await sessions.RevokeAllAsync(login, now, ct);
        throttle.Reset(login);
        await audit.WriteAsync("player", login, "account.password.reset", "game-account", login, client.IpAddress, null, ct);
        return AuthResult.Success;
    }

    public async Task<AuthResult> ConfirmEmailAsync(string? token, ClientInfo client, CancellationToken ct)
    {
        if (!SecureToken.LooksValid(token))
        {
            return AuthResult.Of(AuthStatus.InvalidToken);
        }

        var now = clock.GetUtcNow();
        var login = await tokens.ConsumeAsync(TokenPurpose.EmailConfirm, SecureToken.Hash(token!), now, ct);
        if (login is null)
        {
            return AuthResult.Of(AuthStatus.InvalidToken);
        }

        await players.MarkEmailConfirmedAsync(login, now, ct);
        await audit.WriteAsync("player", login, "account.email.confirmed", "game-account", login, client.IpAddress, null, ct);
        return AuthResult.Success;
    }

    /// <summary>Troca de senha com a sessão aberta. Exige a senha atual e encerra as outras sessões.</summary>
    public async Task<AuthResult> ChangePasswordAsync(string login, string sessionToken, ChangePasswordRequest request, ClientInfo client, CancellationToken ct)
    {
        if (!gameAccounts.WritesEnabled)
        {
            return AuthResult.Of(AuthStatus.Unavailable);
        }

        if (throttle.IsLocked(login, client.IpAddress))
        {
            return AuthResult.Of(AuthStatus.TooManyAttempts);
        }

        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 32
            || (await gameAccounts.CheckPasswordAsync(login, request.CurrentPassword, ct)).Result != GameCredentialResult.Valid)
        {
            throttle.RecordFailure(login, client.IpAddress);
            return new(AuthStatus.InvalidCredentials, [new FieldError(AccountField.Password, "Senha atual incorreta.")]);
        }

        if (AccountRules.ValidatePassword(request.NewPassword, login, O) is { } error)
        {
            return new(AuthStatus.ValidationFailed, [error with { Field = AccountField.NewPassword }]);
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return new(AuthStatus.ValidationFailed, [new FieldError(AccountField.NewPassword, "A nova senha precisa ser diferente da atual.")]);
        }

        if (!await gameAccounts.ChangePasswordAsync(login, request.NewPassword!, ct))
        {
            return AuthResult.Of(AuthStatus.InvalidCredentials);
        }

        await sessions.RevokeAllExceptAsync(login, SecureToken.Hash(sessionToken), clock.GetUtcNow(), ct);
        await audit.WriteAsync("player", login, "account.password.changed", "game-account", login, client.IpAddress, null, ct);
        return AuthResult.Success;
    }

    /// <summary>Cadastra ou troca o e-mail da conta. O novo e-mail precisa ser confirmado.</summary>
    public async Task<RegisterResult> ChangeEmailAsync(string login, ChangeEmailRequest request, ClientInfo client, CancellationToken ct)
    {
        var mail = request.Email?.Trim();
        if (AccountRules.ValidateEmail(mail) is { } error)
        {
            return new(AuthStatus.ValidationFailed, [error], false);
        }

        if (await players.EmailInUseAsync(AccountRules.NormalizeEmail(mail!), login, ct))
        {
            return new(AuthStatus.EmailTaken, null, false);
        }

        var current = await players.FindAsync(login, ct);
        if (current?.Email is not null && AccountRules.NormalizeEmail(current.Email) == AccountRules.NormalizeEmail(mail!) && current.EmailConfirmedUtc is not null)
        {
            return new(AuthStatus.Ok, null, false);
        }

        await players.UpsertAsync(login, mail, null, ct);
        await audit.WriteAsync("player", login, "account.email.changed", "game-account", login, client.IpAddress, null, ct);
        return new(AuthStatus.Ok, null, await SendEmailConfirmationAsync(login, mail!, ct));
    }

    /// <summary>Reenvia o link de confirmação. No máximo 3 por hora.</summary>
    public async Task<AuthResult> ResendEmailConfirmationAsync(string login, CancellationToken ct)
    {
        if (!email.IsConfigured)
        {
            return AuthResult.Of(AuthStatus.Unavailable);
        }

        var account = await players.FindAsync(login, ct);
        if (account?.Email is null || account.EmailConfirmedUtc is not null)
        {
            return AuthResult.Success;
        }

        if (await tokens.CountSinceAsync(TokenPurpose.EmailConfirm, login, clock.GetUtcNow().AddHours(-1), ct) >= MaxRecoveryEmailsPerHour)
        {
            return AuthResult.Of(AuthStatus.TooManyAttempts);
        }

        return await SendEmailConfirmationAsync(login, account.Email, ct) ? AuthResult.Success : AuthResult.Of(AuthStatus.Unavailable);
    }

    public async Task<int> RevokeOtherSessionsAsync(string login, string sessionToken, ClientInfo client, CancellationToken ct)
    {
        var count = await sessions.RevokeAllExceptAsync(login, SecureToken.Hash(sessionToken), clock.GetUtcNow(), ct);
        await audit.WriteAsync("player", login, "account.sessions.revoked", "game-account", login, client.IpAddress, null, ct);
        return count;
    }

    private async Task<bool> SendEmailConfirmationAsync(string login, string address, CancellationToken ct)
    {
        if (!email.IsConfigured)
        {
            return false;
        }

        var (token, hash) = SecureToken.Create();
        await tokens.CreateAsync(TokenPurpose.EmailConfirm, hash, login, clock.GetUtcNow().AddHours(O.EmailConfirmHours), ct);
        var link = $"{O.PublicBaseUrl.TrimEnd('/')}/confirmar-email?token={token}";
        try
        {
            await email.SendAsync(new EmailMessage(
                address,
                "Confirme seu e-mail",
                $"""
                A conta {login} foi criada.

                Confirme seu e-mail para poder recuperar a conta se esquecer a senha:
                {link}

                O link vale por {O.EmailConfirmHours} horas.
                """), ct);
            return true;
        }
#pragma warning disable CA1031 // O cadastro já foi feito; falha de e-mail não pode desfazê-lo.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogEmailFailed(logger, ex, login);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Conta {Login} cadastrada pelo portal")]
    private static partial void LogRegistered(ILogger logger, string login);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao enviar e-mail de confirmação para a conta {Login}")]
    private static partial void LogEmailFailed(ILogger logger, Exception ex, string login);
}
