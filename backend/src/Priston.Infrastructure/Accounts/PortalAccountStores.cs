using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Accounts;

internal static class Utc
{
    public static DateTime From(DateTimeOffset value) => value.UtcDateTime;

    public static DateTimeOffset To(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? To(DateTime? value) => value is null ? null : To(value.Value);
}

internal sealed class PlayerAccountRepository(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IPlayerAccountRepository
{
    public async Task<PlayerAccount?> FindAsync(string gameLogin, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT GameLogin, Email, EmailConfirmedUtc, CreatedUtc, LastLoginUtc
            FROM dbo.PlayerAccount WHERE GameLogin = @gameLogin
            """,
            new { gameLogin = Ansi32(gameLogin) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return row is null
            ? null
            : new PlayerAccount(row.GameLogin, row.Email, Utc.To(row.EmailConfirmedUtc), Utc.To(row.CreatedUtc), Utc.To(row.LastLoginUtc));
    }

    public async Task<bool> EmailInUseAsync(string normalizedEmail, string? exceptLogin, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var hit = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP (1) 1 FROM dbo.PlayerAccount
            WHERE EmailNormalized = @normalizedEmail AND (@exceptLogin IS NULL OR GameLogin <> @exceptLogin)
            """,
            new { normalizedEmail, exceptLogin = new DbString { Value = exceptLogin, IsAnsi = true, Length = 32 } },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return hit.HasValue;
    }

    public async Task UpsertAsync(string gameLogin, string? email, string? ipAddress, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            UPDATE dbo.PlayerAccount WITH (UPDLOCK, SERIALIZABLE)
               SET Email = COALESCE(@email, Email),
                   EmailNormalized = COALESCE(@emailNormalized, EmailNormalized),
                   EmailConfirmedUtc = CASE WHEN @email IS NULL THEN EmailConfirmedUtc ELSE NULL END
             WHERE GameLogin = @gameLogin;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.PlayerAccount (GameLogin, Email, EmailNormalized, CreatedIp)
                VALUES (@gameLogin, @email, @emailNormalized, @ipAddress);
            COMMIT TRANSACTION;
            """,
            new
            {
                gameLogin = Ansi32(gameLogin),
                email,
                emailNormalized = email is null ? null : AccountRules.NormalizeEmail(email),
                ipAddress,
            },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task MarkEmailConfirmedAsync(string gameLogin, DateTimeOffset at, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PlayerAccount SET EmailConfirmedUtc = @at WHERE GameLogin = @gameLogin AND Email IS NOT NULL",
            new { gameLogin = Ansi32(gameLogin), at = Utc.From(at) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task TouchLastLoginAsync(string gameLogin, DateTimeOffset at, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PlayerAccount SET LastLoginUtc = @at WHERE GameLogin = @gameLogin",
            new { gameLogin = Ansi32(gameLogin), at = Utc.From(at) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    internal static DbString Ansi32(string value) => new() { Value = value, IsAnsi = true, Length = 32 };

    private sealed class Row
    {
        public string GameLogin { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime? EmailConfirmedUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }
}

internal sealed class PlayerSessionStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IPlayerSessionStore
{
    public async Task CreateAsync(byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.PlayerSession (TokenHash, GameLogin, ExpiresUtc, IpAddress, UserAgent)
            VALUES (@tokenHash, @gameLogin, @expiresUtc, @ipAddress, @userAgent)
            """,
            new { tokenHash, gameLogin = PlayerAccountRepository.Ansi32(gameLogin), expiresUtc = Utc.From(expiresUtc), ipAddress, userAgent },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<PlayerSession?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<(string GameLogin, DateTime ExpiresUtc)?>(new CommandDefinition(
            """
            SELECT GameLogin, ExpiresUtc FROM dbo.PlayerSession
            WHERE TokenHash = @tokenHash AND RevokedUtc IS NULL AND ExpiresUtc > @now
            """,
            new { tokenHash, now = Utc.From(now) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return row is { } r ? new PlayerSession(r.GameLogin, Utc.To(r.ExpiresUtc)) : null;
    }

    public async Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PlayerSession SET RevokedUtc = @now WHERE TokenHash = @tokenHash AND RevokedUtc IS NULL",
            new { tokenHash, now = Utc.From(now) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task RevokeAllAsync(string gameLogin, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PlayerSession SET RevokedUtc = @now WHERE GameLogin = @gameLogin AND RevokedUtc IS NULL",
            new { gameLogin = PlayerAccountRepository.Ansi32(gameLogin), now = Utc.From(now) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }
}

internal sealed class OneTimeTokenStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IOneTimeTokenStore
{
    public async Task CreateAsync(string purpose, byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.OneTimeToken (Purpose, TokenHash, GameLogin, ExpiresUtc)
            VALUES (@purpose, @tokenHash, @gameLogin, @expiresUtc)
            """,
            new { purpose, tokenHash, gameLogin = PlayerAccountRepository.Ansi32(gameLogin), expiresUtc = Utc.From(expiresUtc) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<string?> PeekAsync(string purpose, byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT GameLogin FROM dbo.OneTimeToken
            WHERE TokenHash = @tokenHash AND Purpose = @purpose AND UsedUtc IS NULL AND ExpiresUtc > @now
            """,
            new { purpose, tokenHash, now = Utc.From(now) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<string?> ConsumeAsync(string purpose, byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            UPDATE dbo.OneTimeToken SET UsedUtc = @now
            OUTPUT inserted.GameLogin
            WHERE TokenHash = @tokenHash AND Purpose = @purpose AND UsedUtc IS NULL AND ExpiresUtc > @now
            """,
            new { purpose, tokenHash, now = Utc.From(now) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<int> CountSinceAsync(string purpose, string gameLogin, DateTimeOffset since, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.OneTimeToken WHERE Purpose = @purpose AND GameLogin = @gameLogin AND CreatedUtc >= @since",
            new { purpose, gameLogin = PlayerAccountRepository.Ansi32(gameLogin), since = Utc.From(since) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }
}

internal sealed class AuditLogWriter(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IAuditLog
{
    public async Task WriteAsync(string actorType, string? actorId, string action, string? targetType, string? targetId, string? ipAddress, string? details, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.AuditLog (ActorType, ActorId, Action, TargetType, TargetId, IpAddress, AfterJson)
            VALUES (@actorType, @actorId, @action, @targetType, @targetId, @ipAddress, @details)
            """,
            new { actorType, actorId, action, targetType, targetId, ipAddress, details },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
    }
}
