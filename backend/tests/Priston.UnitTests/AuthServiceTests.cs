using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;

namespace Priston.UnitTests;

public class AuthServiceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeGame : IGameAccountStore
    {
        public Dictionary<string, (string Password, bool Blocked)> Accounts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool WritesEnabled { get; set; } = true;

        public Task<bool> ExistsAsync(string login, CancellationToken ct) => Task.FromResult(Accounts.ContainsKey(login));

        public Task<GameCredentialCheck> CheckPasswordAsync(string login, string password, CancellationToken ct)
        {
            if (!Accounts.TryGetValue(login, out var a))
            {
                return Task.FromResult(new GameCredentialCheck(GameCredentialResult.NotFound, null));
            }

            var canonical = Accounts.Keys.First(k => string.Equals(k, login, StringComparison.OrdinalIgnoreCase));
            if (a.Password != password)
            {
                return Task.FromResult(new GameCredentialCheck(GameCredentialResult.Invalid, canonical));
            }

            return Task.FromResult(new GameCredentialCheck(a.Blocked ? GameCredentialResult.Blocked : GameCredentialResult.Valid, canonical));
        }

        public Task<GameAccountCreateResult> CreateAsync(string login, string password, string? ipAddress, CancellationToken ct)
        {
            if (!Accounts.TryAdd(login, (password, false)))
            {
                return Task.FromResult(GameAccountCreateResult.AlreadyExists);
            }

            return Task.FromResult(GameAccountCreateResult.Created);
        }

        public Task<bool> ChangePasswordAsync(string login, string newPassword, CancellationToken ct)
        {
            if (!Accounts.TryGetValue(login, out var a))
            {
                return Task.FromResult(false);
            }

            Accounts[login] = (newPassword, a.Blocked);
            return Task.FromResult(true);
        }
    }

    private sealed class FakePlayers : IPlayerAccountRepository
    {
        public Dictionary<string, PlayerAccount> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<PlayerAccount?> FindAsync(string gameLogin, CancellationToken ct) =>
            Task.FromResult(Rows.GetValueOrDefault(gameLogin));

        public Task<bool> EmailInUseAsync(string normalizedEmail, string? exceptLogin, CancellationToken ct) =>
            Task.FromResult(Rows.Values.Any(r => r.Email is not null && AccountRules.NormalizeEmail(r.Email) == normalizedEmail && r.GameLogin != exceptLogin));

        public Task UpsertAsync(string gameLogin, string? email, string? ipAddress, CancellationToken ct)
        {
            if (Rows.TryGetValue(gameLogin, out var row))
            {
                Rows[gameLogin] = email is null ? row : row with { Email = email, EmailConfirmedUtc = null };
            }
            else
            {
                Rows[gameLogin] = new PlayerAccount(gameLogin, email, null, DateTimeOffset.UnixEpoch, null);
            }

            return Task.CompletedTask;
        }

        public Task MarkEmailConfirmedAsync(string gameLogin, DateTimeOffset at, CancellationToken ct)
        {
            Rows[gameLogin] = Rows[gameLogin] with { EmailConfirmedUtc = at };
            return Task.CompletedTask;
        }

        public Task TouchLastLoginAsync(string gameLogin, DateTimeOffset at, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSessions : IPlayerSessionStore
    {
        public List<(byte[] Hash, string Login, DateTimeOffset Expires, bool Revoked)> Rows { get; } = [];

        public Task CreateAsync(byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            Rows.Add((tokenHash, gameLogin, expiresUtc, false));
            return Task.CompletedTask;
        }

        public Task<PlayerSession?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
        {
            var r = Rows.FirstOrDefault(r => r.Hash.SequenceEqual(tokenHash) && !r.Revoked && r.Expires > now);
            return Task.FromResult(r.Login is null ? null : new PlayerSession(r.Login, r.Expires));
        }

        public Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
        {
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Hash.SequenceEqual(tokenHash))
                {
                    Rows[i] = Rows[i] with { Revoked = true };
                }
            }

            return Task.CompletedTask;
        }

        public Task<int> RevokeAllExceptAsync(string gameLogin, byte[] keepTokenHash, DateTimeOffset now, CancellationToken ct)
        {
            var n = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Login == gameLogin && !Rows[i].Revoked && !Rows[i].Hash.SequenceEqual(keepTokenHash))
                {
                    Rows[i] = Rows[i] with { Revoked = true };
                    n++;
                }
            }

            return Task.FromResult(n);
        }

        public Task RevokeAllAsync(string gameLogin, DateTimeOffset now, CancellationToken ct)
        {
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Login == gameLogin)
                {
                    Rows[i] = Rows[i] with { Revoked = true };
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokens : IOneTimeTokenStore
    {
        public List<(string Purpose, byte[] Hash, string Login, DateTimeOffset Created, DateTimeOffset Expires, bool Used)> Rows { get; } = [];

        public DateTimeOffset Now { get; set; }

        public Task CreateAsync(string purpose, byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, CancellationToken ct)
        {
            Rows.Add((purpose, tokenHash, gameLogin, Now, expiresUtc, false));
            return Task.CompletedTask;
        }

        public Task<string?> PeekAsync(string purpose, byte[] tokenHash, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(Find(purpose, tokenHash, now) is var i and >= 0 ? Rows[i].Login : null);

        public Task<string?> ConsumeAsync(string purpose, byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
        {
            var i = Find(purpose, tokenHash, now);
            if (i < 0)
            {
                return Task.FromResult<string?>(null);
            }

            Rows[i] = Rows[i] with { Used = true };
            return Task.FromResult<string?>(Rows[i].Login);
        }

        public Task<int> CountSinceAsync(string purpose, string gameLogin, DateTimeOffset since, CancellationToken ct) =>
            Task.FromResult(Rows.Count(r => r.Purpose == purpose && r.Login == gameLogin && r.Created >= since));

        private int Find(string purpose, byte[] hash, DateTimeOffset now) =>
            Rows.FindIndex(r => r.Purpose == purpose && r.Hash.SequenceEqual(hash) && !r.Used && r.Expires > now);
    }

    private sealed class FakeEmail : IEmailSender
    {
        public bool IsConfigured { get; set; } = true;

        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }

        public string LastToken() => Sent[^1].TextBody.Split("token=")[1].Split('\n')[0].Trim();
    }

    private sealed class NoCaptcha : ICaptchaVerifier
    {
        public bool IsEnabled => false;

        public Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class NoAudit : IAuditLog
    {
        public List<string> Actions { get; } = [];

        public Task WriteAsync(string actorType, string? actorId, string action, string? targetType, string? targetId, string? ipAddress, string? details, CancellationToken ct)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness : IDisposable
    {
        public Clock Clock { get; } = new();
        public FakeGame Game { get; } = new();
        public FakePlayers Players { get; } = new();
        public FakeSessions Sessions { get; } = new();
        public FakeTokens Tokens { get; } = new();
        public FakeEmail Email { get; } = new();
        public NoAudit Audit { get; } = new();
        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());
        public AuthService Auth { get; }

        public Harness()
        {
            var options = Options.Create(new AccountOptions { PublicBaseUrl = "https://portal.example" });
            Tokens.Now = Clock.Now;
            Auth = new AuthService(Game, Players, Sessions, Tokens, Email, new NoCaptcha(), Audit,
                new LoginThrottle(Cache, options, Clock), options, Clock, NullLogger<AuthService>.Instance);
        }

        public void Dispose() => Cache.Dispose();
    }

    private static readonly ClientInfo Client = new("203.0.113.9", "teste");

    [Fact]
    public async Task Register_creates_game_account_and_sends_confirmation()
    {
        using var h = new Harness();

        var result = await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);

        Assert.Equal(AuthStatus.Ok, result.Status);
        Assert.True(result.ConfirmationEmailSent);
        Assert.Equal("senha123", h.Game.Accounts["alpha01"].Password);
        Assert.Equal("alpha@example.com", h.Players.Rows["alpha01"].Email);
        Assert.Contains("https://portal.example/confirmar-email?token=", h.Email.Sent[0].TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("senha123", h.Email.Sent[0].TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_is_unavailable_without_write_login()
    {
        using var h = new Harness();
        h.Game.WritesEnabled = false;

        var result = await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);

        Assert.Equal(AuthStatus.Unavailable, result.Status);
        Assert.Empty(h.Game.Accounts);
    }

    [Fact]
    public async Task Register_rejects_taken_login_case_insensitively()
    {
        using var h = new Harness();
        h.Game.Accounts["Alpha01"] = ("outra123", false);

        var result = await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);

        Assert.Equal(AuthStatus.LoginTaken, result.Status);
    }

    [Fact]
    public async Task Register_rejects_email_of_another_account()
    {
        using var h = new Harness();
        await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);

        var result = await h.Auth.RegisterAsync(new("bravo02", "senha456", "ALPHA@example.com", null), Client, default);

        Assert.Equal(AuthStatus.EmailTaken, result.Status);
        Assert.False(h.Game.Accounts.ContainsKey("bravo02"));
    }

    [Fact]
    public async Task Register_returns_field_errors()
    {
        using var h = new Harness();

        var result = await h.Auth.RegisterAsync(new("a!", "123", "nao-e-email", null), Client, default);

        Assert.Equal(AuthStatus.ValidationFailed, result.Status);
        Assert.Equal(3, result.Errors!.Count);
    }

    [Fact]
    public async Task Login_returns_session_that_resolves_to_player()
    {
        using var h = new Harness();
        h.Game.Accounts["Alpha01"] = ("senha123", false);

        var login = await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default);
        var me = await h.Auth.GetCurrentAsync(login.SessionToken, default);

        Assert.Equal(AuthStatus.Ok, login.Status);
        Assert.Equal("Alpha01", me!.Login);
        Assert.Equal(h.Clock.Now.AddHours(12), login.ExpiresUtc);
        Assert.DoesNotContain(h.Sessions.Rows, r => System.Text.Encoding.UTF8.GetString(r.Hash).Contains(login.SessionToken!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_revokes_session()
    {
        using var h = new Harness();
        h.Game.Accounts["alpha01"] = ("senha123", false);
        var login = await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default);

        await h.Auth.LogoutAsync(login.SessionToken, default);

        Assert.Null(await h.Auth.GetCurrentAsync(login.SessionToken, default));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_login_look_the_same()
    {
        using var h = new Harness();
        h.Game.Accounts["alpha01"] = ("senha123", false);

        var wrong = await h.Auth.LoginAsync(new("alpha01", "errada99", null), Client, default);
        var unknown = await h.Auth.LoginAsync(new("ninguem", "errada99", null), Client, default);

        Assert.Equal(AuthStatus.InvalidCredentials, wrong.Status);
        Assert.Equal(AuthStatus.InvalidCredentials, unknown.Status);
    }

    [Fact]
    public async Task Blocked_account_is_reported_only_with_correct_password()
    {
        using var h = new Harness();
        h.Game.Accounts["alpha01"] = ("senha123", true);

        Assert.Equal(AuthStatus.InvalidCredentials, (await h.Auth.LoginAsync(new("alpha01", "errada99", null), Client, default)).Status);
        Assert.Equal(AuthStatus.Blocked, (await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default)).Status);
    }

    [Fact]
    public async Task Five_failures_lock_the_login_until_window_passes()
    {
        using var h = new Harness();
        h.Game.Accounts["alpha01"] = ("senha123", false);
        for (var i = 0; i < 5; i++)
        {
            await h.Auth.LoginAsync(new("alpha01", "errada99", null), Client, default);
        }

        Assert.Equal(AuthStatus.TooManyAttempts, (await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default)).Status);

        h.Clock.Now = h.Clock.Now.AddMinutes(16);
        Assert.Equal(AuthStatus.Ok, (await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default)).Status);
    }

    [Fact]
    public async Task Recovery_flow_changes_password_and_revokes_sessions()
    {
        using var h = new Harness();
        await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);
        await h.Auth.ConfirmEmailAsync(h.Email.LastToken(), Client, default);
        var session = await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default);

        var request = await h.Auth.RequestPasswordRecoveryAsync(new("alpha01", "alpha@example.com", null), Client, default);
        var reset = await h.Auth.ResetPasswordAsync(new(h.Email.LastToken(), "novasenha7"), Client, default);

        Assert.Equal(AuthStatus.Ok, request.Status);
        Assert.Equal(AuthStatus.Ok, reset.Status);
        Assert.Equal("novasenha7", h.Game.Accounts["alpha01"].Password);
        Assert.Null(await h.Auth.GetCurrentAsync(session.SessionToken, default));
        Assert.Equal(AuthStatus.InvalidToken, (await h.Auth.ResetPasswordAsync(new(h.Email.LastToken(), "outra1234"), Client, default)).Status);
    }

    [Fact]
    public async Task Recovery_does_not_send_when_email_is_unconfirmed_or_wrong()
    {
        using var h = new Harness();
        await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);
        var sentAfterRegister = h.Email.Sent.Count;

        var unconfirmed = await h.Auth.RequestPasswordRecoveryAsync(new("alpha01", "alpha@example.com", null), Client, default);
        await h.Auth.ConfirmEmailAsync(h.Email.LastToken(), Client, default);
        var wrong = await h.Auth.RequestPasswordRecoveryAsync(new("alpha01", "outro@example.com", null), Client, default);

        Assert.Equal(AuthStatus.Ok, unconfirmed.Status);
        Assert.Equal(AuthStatus.Ok, wrong.Status);
        Assert.Equal(sentAfterRegister, h.Email.Sent.Count);
    }

    [Fact]
    public async Task Expired_reset_token_is_rejected()
    {
        using var h = new Harness();
        await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);
        await h.Auth.ConfirmEmailAsync(h.Email.LastToken(), Client, default);
        await h.Auth.RequestPasswordRecoveryAsync(new("alpha01", "alpha@example.com", null), Client, default);

        h.Clock.Now = h.Clock.Now.AddMinutes(31);

        Assert.Equal(AuthStatus.InvalidToken, (await h.Auth.ResetPasswordAsync(new(h.Email.LastToken(), "novasenha7"), Client, default)).Status);
        Assert.Equal("senha123", h.Game.Accounts["alpha01"].Password);
    }

    [Fact]
    public async Task Change_password_requires_current_and_keeps_only_this_session()
    {
        using var h = new Harness();
        h.Game.Accounts["alpha01"] = ("senha123", false);
        var a = await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default);
        var b = await h.Auth.LoginAsync(new("alpha01", "senha123", null), Client, default);

        var wrong = await h.Auth.ChangePasswordAsync("alpha01", a.SessionToken!, new("errada99", "novasenha7"), Client, default);
        var same = await h.Auth.ChangePasswordAsync("alpha01", a.SessionToken!, new("senha123", "senha123"), Client, default);
        var ok = await h.Auth.ChangePasswordAsync("alpha01", a.SessionToken!, new("senha123", "novasenha7"), Client, default);

        Assert.Equal(AuthStatus.InvalidCredentials, wrong.Status);
        Assert.Equal(AuthStatus.ValidationFailed, same.Status);
        Assert.Equal(AuthStatus.Ok, ok.Status);
        Assert.Equal("novasenha7", h.Game.Accounts["alpha01"].Password);
        Assert.NotNull(await h.Auth.GetCurrentAsync(a.SessionToken, default));
        Assert.Null(await h.Auth.GetCurrentAsync(b.SessionToken, default));
    }

    [Fact]
    public async Task Change_email_resets_confirmation_and_rejects_email_of_other_account()
    {
        using var h = new Harness();
        await h.Auth.RegisterAsync(new("alpha01", "senha123", "alpha@example.com", null), Client, default);
        await h.Auth.RegisterAsync(new("bravo02", "senha456", "bravo@example.com", null), Client, default);
        await h.Auth.ConfirmEmailAsync(h.Email.LastToken(), Client, default);

        var taken = await h.Auth.ChangeEmailAsync("bravo02", new("ALPHA@example.com"), Client, default);
        var changed = await h.Auth.ChangeEmailAsync("bravo02", new("novo@example.com"), Client, default);

        Assert.Equal(AuthStatus.EmailTaken, taken.Status);
        Assert.Equal(AuthStatus.Ok, changed.Status);
        Assert.True(changed.ConfirmationEmailSent);
        Assert.Equal("novo@example.com", h.Players.Rows["bravo02"].Email);
        Assert.Null(h.Players.Rows["bravo02"].EmailConfirmedUtc);
    }
}
