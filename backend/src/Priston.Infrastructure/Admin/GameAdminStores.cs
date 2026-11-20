using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Admin;
using Priston.Application.GameAdmin;
using Priston.Infrastructure.Accounts;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Admin;

internal sealed class GameBanStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IGameBanStore
{
    private const string Columns = "Id, GameLogin, Reason, BannedBy, BannedUtc, ExpiresUtc, LiftedUtc, LiftedBy, LiftReason";

    public async Task<long> CreateAsync(string login, string reason, string bannedBy, DateTimeOffset now, DateTimeOffset? expiresUtc, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<long>(Cmd(
            """
            INSERT INTO dbo.GameBan (GameLogin, Reason, BannedBy, BannedUtc, ExpiresUtc)
            OUTPUT INSERTED.Id VALUES (@login, @reason, @bannedBy, @now, @expires)
            """,
            new { login = Ansi(login, 32), reason, bannedBy = Ansi(bannedBy, 32), now = Utc.From(now), expires = expiresUtc?.UtcDateTime }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameBanRecord>> ListAsync(string login, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd($"SELECT TOP (50) {Columns} FROM dbo.GameBan WHERE GameLogin = @login ORDER BY BannedUtc DESC, Id DESC",
            new { login = Ansi(login, 32) }, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task LiftActiveAsync(string login, string liftedBy, string reason, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            "UPDATE dbo.GameBan SET LiftedUtc = @now, LiftedBy = @by, LiftReason = @reason WHERE GameLogin = @login AND LiftedUtc IS NULL",
            new { login = Ansi(login, 32), by = Ansi(liftedBy, 32), reason, now = Utc.From(now) }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameBanRecord>> ListExpiredActiveAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd($"SELECT TOP (200) {Columns} FROM dbo.GameBan WHERE LiftedUtc IS NULL AND ExpiresUtc IS NOT NULL AND ExpiresUtc <= @now",
            new { now = Utc.From(now) }, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string GameLogin { get; init; } = "";
        public string Reason { get; init; } = "";
        public string BannedBy { get; init; } = "";
        public DateTime BannedUtc { get; init; }
        public DateTime? ExpiresUtc { get; init; }
        public DateTime? LiftedUtc { get; init; }
        public string? LiftedBy { get; init; }
        public string? LiftReason { get; init; }

        public GameBanRecord ToModel() => new(Id, GameLogin, Reason, BannedBy, Utc.To(BannedUtc), Utc.To(ExpiresUtc), Utc.To(LiftedUtc), LiftedBy, LiftReason);
    }
}

internal sealed class AccountActivityReader(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IAccountActivityReader
{
    public async Task<IReadOnlyList<AuditEntry>> ListForAccountAsync(string login, int take, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd(
            """
            SELECT TOP (@take) Id, OccurredUtc, ActorType, ActorId, Action, TargetType, TargetId, IpAddress, AfterJson AS Details
            FROM dbo.AuditLog
            WHERE TargetType = 'game-account' AND TargetId = @login
            ORDER BY OccurredUtc DESC, Id DESC
            """, new { take, login }, ct)).ConfigureAwait(false);
        return rows.Select(r => new AuditEntry(r.Id, Utc.To(r.OccurredUtc), r.ActorType, r.ActorId, r.Action, r.TargetType, r.TargetId, r.IpAddress, r.Details)).ToList();
    }

    public async Task<int> CountActivePlayerSessionsAsync(string login, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(Cmd(
            "SELECT COUNT(*) FROM dbo.PlayerSession WHERE GameLogin = @login AND RevokedUtc IS NULL AND ExpiresUtc > @now",
            new { login = Ansi(login, 32), now = Utc.From(now) }, ct)).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public DateTime OccurredUtc { get; init; }
        public string ActorType { get; init; } = "";
        public string? ActorId { get; init; }
        public string Action { get; init; } = "";
        public string? TargetType { get; init; }
        public string? TargetId { get; init; }
        public string? IpAddress { get; init; }
        public string? Details { get; init; }
    }
}

internal sealed class RankingBackupStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IRankingBackupStore
{
    public async Task<long> CreateAsync(string kind, string createdBy, DateTimeOffset now, int rows, string json, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<long>(Cmd(
            "INSERT INTO dbo.RankingResetBackup (Kind, CreatedBy, CreatedUtc, RowsCount, Data) OUTPUT INSERTED.Id VALUES (@kind, @createdBy, @now, @rows, @json)",
            new { kind = Ansi(kind, 32), createdBy = Ansi(createdBy, 32), now = Utc.From(now), rows, json }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RankingBackup>> ListAsync(int take, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd(
            "SELECT TOP (@take) Id, Kind, CreatedBy, CreatedUtc, RowsCount, RestoredUtc, RestoredBy FROM dbo.RankingResetBackup ORDER BY CreatedUtc DESC, Id DESC",
            new { take }, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<(RankingBackup Info, string Json)?> GetAsync(long id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<Row>(Cmd(
            "SELECT Id, Kind, CreatedBy, CreatedUtc, RowsCount, RestoredUtc, RestoredBy, Data FROM dbo.RankingResetBackup WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
        return row is null ? null : (row.ToModel(), row.Data ?? "[]");
    }

    public async Task MarkRestoredAsync(long id, string by, DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd("UPDATE dbo.RankingResetBackup SET RestoredUtc = @now, RestoredBy = @by WHERE Id = @id",
            new { id, by = Ansi(by, 32), now = Utc.From(now) }, ct)).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string Kind { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public DateTime CreatedUtc { get; init; }
        public int RowsCount { get; init; }
        public DateTime? RestoredUtc { get; init; }
        public string? RestoredBy { get; init; }
        public string? Data { get; init; }

        public RankingBackup ToModel() => new(Id, Kind, CreatedBy, Utc.To(CreatedUtc), RowsCount, Utc.To(RestoredUtc), RestoredBy);
    }
}

internal sealed class GameSourceBindingStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IGameSourceBindingStore
{
    private const string Columns = "FunctionKey, DatabaseName, TableName, KeyColumns, EditableColumns, SearchColumns, AccountColumn, AllowInsert, AllowDelete, NewestFirst, UpdatedBy, UpdatedUtc";

    public async Task<IReadOnlyList<GameSourceBinding>> ListAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd($"SELECT {Columns} FROM dbo.GameSourceBinding", null, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<GameSourceBinding?> GetAsync(string functionKey, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QueryFirstOrDefaultAsync<Row>(Cmd($"SELECT {Columns} FROM dbo.GameSourceBinding WHERE FunctionKey = @key",
            new { key = Ansi(functionKey, 32) }, ct)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task SaveAsync(GameSourceBinding b, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            """
            MERGE dbo.GameSourceBinding WITH (HOLDLOCK) AS t
            USING (SELECT @key AS FunctionKey) AS s ON t.FunctionKey = s.FunctionKey
            WHEN MATCHED THEN UPDATE SET DatabaseName = @db, TableName = @table, KeyColumns = @keys, EditableColumns = @editable, SearchColumns = @search,
                AccountColumn = @account, AllowInsert = @allowInsert, AllowDelete = @allowDelete, NewestFirst = @newestFirst, UpdatedBy = @by, UpdatedUtc = @at
            WHEN NOT MATCHED THEN INSERT (FunctionKey, DatabaseName, TableName, KeyColumns, EditableColumns, SearchColumns, AccountColumn, AllowInsert, AllowDelete, NewestFirst, UpdatedBy, UpdatedUtc)
                VALUES (@key, @db, @table, @keys, @editable, @search, @account, @allowInsert, @allowDelete, @newestFirst, @by, @at);
            """,
            new
            {
                key = Ansi(b.FunctionKey, 32),
                db = b.Database,
                table = b.Table,
                keys = JsonSerializer.Serialize(b.KeyColumns),
                editable = JsonSerializer.Serialize(b.EditableColumns),
                search = JsonSerializer.Serialize(b.SearchColumns),
                account = b.AccountColumn,
                allowInsert = b.AllowInsert,
                allowDelete = b.AllowDelete,
                newestFirst = b.NewestFirst,
                by = Ansi(b.UpdatedBy, 32),
                at = Utc.From(b.UpdatedUtc),
            }, ct)).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string functionKey, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd("DELETE FROM dbo.GameSourceBinding WHERE FunctionKey = @key", new { key = Ansi(functionKey, 32) }, ct)).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public string FunctionKey { get; init; } = "";
        public string DatabaseName { get; init; } = "";
        public string TableName { get; init; } = "";
        public string KeyColumns { get; init; } = "[]";
        public string EditableColumns { get; init; } = "[]";
        public string SearchColumns { get; init; } = "[]";
        public string? AccountColumn { get; init; }
        public bool AllowInsert { get; init; }
        public bool AllowDelete { get; init; }
        public bool NewestFirst { get; init; }
        public string UpdatedBy { get; init; } = "";
        public DateTime UpdatedUtc { get; init; }

        private static List<string> List(string json) => JsonSerializer.Deserialize<List<string>>(json) ?? [];

        public GameSourceBinding ToModel() => new(FunctionKey, DatabaseName, TableName, List(KeyColumns), List(EditableColumns), List(SearchColumns),
            AccountColumn, AllowInsert, AllowDelete, NewestFirst, UpdatedBy, Utc.To(UpdatedUtc));
    }
}
