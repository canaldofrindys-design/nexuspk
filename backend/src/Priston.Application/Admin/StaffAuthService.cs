using System.Text.RegularExpressions;
using Priston.Application.Accounts;

namespace Priston.Application.Admin;

public enum StaffAuthStatus
{
    Ok,
    InvalidCredentials,
    Locked,
    TooManyAttempts,
    ValidationFailed,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record StaffLoginRequest(string? Username, string? Password);

public sealed record StaffLoginResult(StaffAuthStatus Status, string? Token, DateTimeOffset? ExpiresUtc, bool MustChangePassword);

public sealed record StaffChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record StaffResult(StaffAuthStatus Status, IReadOnlyDictionary<string, string>? Errors = null)
{
    public static StaffResult Ok { get; } = new(StaffAuthStatus.Ok);

    public static StaffResult Of(StaffAuthStatus status) => new(status);

    public static StaffResult Invalid(string field, string message) => new(StaffAuthStatus.ValidationFailed, new Dictionary<string, string> { [field] = message });
}

/// <summary>
/// Login da equipe. Contas e sessões ficam no banco do portal, separadas dos jogadores.
/// Cinco senhas erradas bloqueiam a conta por 15 minutos. Nada de senha ou token vai para o log.
/// </summary>
public sealed partial class StaffAuthService(
    IStaffRepository staff,
    IStaffSessionStore sessions,
    IAuditLog audit,
    LoginThrottle throttle,
    TimeProvider clock)
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public async Task<StaffLoginResult> LoginAsync(StaffLoginRequest request, ClientInfo client, CancellationToken ct)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (!UsernamePattern().IsMatch(username) || password.Length is 0 or > StaffPassword.MaxLength)
        {
            return new(StaffAuthStatus.InvalidCredentials, null, null, false);
        }

        var key = "staff:" + username;
        if (throttle.IsLocked(key, client.IpAddress))
        {
            return new(StaffAuthStatus.TooManyAttempts, null, null, false);
        }

        var now = clock.GetUtcNow();
        var user = await staff.FindByUsernameAsync(username, ct).ConfigureAwait(false);
        var valid = StaffPassword.Verify(password, user?.PasswordHash ?? StaffPassword.Dummy);
        if (user is null || !user.Active)
        {
            throttle.RecordFailure(key, client.IpAddress);
            await audit.WriteAsync("staff", username, "staff.login-failed", null, null, client.IpAddress, null, ct).ConfigureAwait(false);
            return new(StaffAuthStatus.InvalidCredentials, null, null, false);
        }

        if (user.LockedUntilUtc > now)
        {
            return new(StaffAuthStatus.Locked, null, null, false);
        }

        if (!valid)
        {
            throttle.RecordFailure(key, client.IpAddress);
            var lockUntil = user.FailedLogins + 1 >= MaxFailures ? now + LockDuration : (DateTimeOffset?)null;
            await staff.RecordFailureAsync(user.Id, lockUntil, ct).ConfigureAwait(false);
            await audit.WriteAsync("staff", user.Username, lockUntil is null ? "staff.login-failed" : "staff.locked", "staff", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), client.IpAddress, null, ct).ConfigureAwait(false);
            return new(lockUntil is null ? StaffAuthStatus.InvalidCredentials : StaffAuthStatus.Locked, null, null, false);
        }

        throttle.Reset(key);
        var (token, hash) = SecureToken.Create();
        var expires = now + SessionLifetime;
        await sessions.CreateAsync(hash, user.Id, expires, client.IpAddress, client.UserAgent, ct).ConfigureAwait(false);
        await staff.RecordLoginAsync(user.Id, now, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", user.Username, "staff.login", null, null, client.IpAddress, null, ct).ConfigureAwait(false);
        return new(StaffAuthStatus.Ok, token, expires, user.MustChangePassword);
    }

    public async Task<StaffPrincipal?> GetCurrentAsync(string? token, CancellationToken ct)
    {
        if (!SecureToken.LooksValid(token))
        {
            return null;
        }

        var session = await sessions.FindActiveAsync(SecureToken.Hash(token!), clock.GetUtcNow(), ct).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        var user = await staff.FindByIdAsync(session.Value.StaffId, ct).ConfigureAwait(false);
        return user is { Active: true }
            ? new StaffPrincipal(user.Id, user.Username, user.DisplayName, user.Role, user.MustChangePassword, session.Value.ExpiresUtc)
            : null;
    }

    public async Task LogoutAsync(string? token, CancellationToken ct)
    {
        if (SecureToken.LooksValid(token))
        {
            await sessions.RevokeAsync(SecureToken.Hash(token!), clock.GetUtcNow(), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Troca a própria senha. Encerra as outras sessões do mesmo usuário.</summary>
    public async Task<StaffResult> ChangePasswordAsync(StaffPrincipal me, string token, StaffChangePasswordRequest request, ClientInfo client, CancellationToken ct)
    {
        var user = await staff.FindByIdAsync(me.Id, ct).ConfigureAwait(false);
        if (user is null)
        {
            return StaffResult.Of(StaffAuthStatus.NotFound);
        }

        if (!StaffPassword.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            return StaffResult.Invalid("currentPassword", "Senha atual incorreta.");
        }

        if (StaffPassword.Validate(request.NewPassword, user.Username) is { } error)
        {
            return StaffResult.Invalid("newPassword", error);
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return StaffResult.Invalid("newPassword", "A nova senha deve ser diferente da atual.");
        }

        var now = clock.GetUtcNow();
        await staff.SetPasswordAsync(user.Id, StaffPassword.Hash(request.NewPassword!), false, now, ct).ConfigureAwait(false);
        await sessions.RevokeAllAsync(user.Id, SecureToken.Hash(token), now, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", user.Username, "staff.password-changed", "staff", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), client.IpAddress, null, ct).ConfigureAwait(false);
        return StaffResult.Ok;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,32}$", RegexOptions.CultureInvariant)]
    internal static partial Regex UsernamePattern();
}
