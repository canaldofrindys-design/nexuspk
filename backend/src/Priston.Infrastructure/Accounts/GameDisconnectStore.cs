using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Player;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Accounts;

/// <summary>PortalDB.dbo.GameDisconnectRequest e GameServerRuntime (migração 0013).</summary>
internal sealed class GameDisconnectStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IGameDisconnectStore
{
    private int Timeout => options.Value.CommandTimeoutSeconds;

    private static DbString Login(string value) => new() { Value = value, IsAnsi = true, Length = 32 };

    public async Task<DisconnectRuntime?> RuntimeAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<RuntimeRow>(new CommandDefinition(
            "SELECT ProtocolVersion, LastSeenUtc FROM dbo.GameServerRuntime WHERE Feature = 'disconnect'",
            commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return row is null ? null : new DisconnectRuntime(row.ProtocolVersion, Utc.To(row.LastSeenUtc));
    }

    public async Task<DisconnectRequestRow?> LatestAsync(string gameLogin, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<RequestRow>(new CommandDefinition(
            """
            SELECT TOP (1) Id, RequestedUtc, Status, ProcessedUtc, ServerNote
            FROM dbo.GameDisconnectRequest WHERE GameLogin = @login
            ORDER BY RequestedUtc DESC, Id DESC
            """,
            new { login = Login(gameLogin) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return row is null ? null : new DisconnectRequestRow(row.Id, Utc.To(row.RequestedUtc), row.Status, Utc.To(row.ProcessedUtc), row.ServerNote);
    }

    public async Task<int> CountSinceAsync(string gameLogin, DateTimeOffset sinceUtc, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.GameDisconnectRequest WHERE GameLogin = @login AND RequestedUtc >= @since",
            new { login = Login(gameLogin), since = Utc.From(sinceUtc) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<long?> TryCreateAsync(string gameLogin, DateTimeOffset nowUtc, DateTimeOffset pendingSinceUtc, string? ipAddress, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            IF EXISTS (SELECT 1 FROM dbo.GameDisconnectRequest WITH (UPDLOCK, HOLDLOCK)
                       WHERE GameLogin = @login AND Status = 'pending' AND RequestedUtc > @pendingSince)
            BEGIN
                COMMIT;
                SELECT CAST(NULL AS bigint);
                RETURN;
            END
            INSERT INTO dbo.GameDisconnectRequest (GameLogin, RequestedUtc, RequestedIp, Status)
            VALUES (@login, @now, @ip, 'pending');
            SELECT CAST(SCOPE_IDENTITY() AS bigint);
            COMMIT;
            """,
            new
            {
                login = Login(gameLogin),
                now = Utc.From(nowUtc),
                pendingSince = Utc.From(pendingSinceUtc),
                ip = new DbString { Value = ipAddress is { Length: > 45 } ? ipAddress[..45] : ipAddress, IsAnsi = true, Length = 45 },
            },
            commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed record RuntimeRow(int ProtocolVersion, DateTime? LastSeenUtc);

    private sealed record RequestRow(long Id, DateTime RequestedUtc, string Status, DateTime? ProcessedUtc, string? ServerNote);
}
