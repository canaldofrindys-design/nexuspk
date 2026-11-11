using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Server;
using Priston.Infrastructure.Accounts;
using Priston.Infrastructure.Admin;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.GameServer;

internal sealed class ServerProbeStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IServerProbeStore
{
    public async Task RecordAsync(ServerSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot.CheckedAtUtc is null || snapshot.Targets.Count == 0)
        {
            return;
        }

        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var at = Utc.From(snapshot.CheckedAtUtc.Value);
        await conn.ExecuteAsync(Cmd(
            "INSERT INTO dbo.ServerProbe (TargetKey, CheckedUtc, Online, LatencyMs) VALUES (@Key, @at, @Online, @LatencyMs)",
            snapshot.Targets.Select(t => new { Key = Ansi(t.Key, 32), at, t.Online, t.LatencyMs }).ToList(), ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProbeHour>> HourlyAsync(DateTimeOffset sinceUtc, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<HourRow>(Cmd(
            """
            SELECT TargetKey,
                   DATEADD(hour, DATEDIFF(hour, 0, CheckedUtc), 0) AS HourUtc,
                   COUNT(*) AS Samples,
                   SUM(CASE WHEN Online = 1 THEN 1 ELSE 0 END) AS OnlineSamples
            FROM dbo.ServerProbe
            WHERE CheckedUtc >= @since
            GROUP BY TargetKey, DATEADD(hour, DATEDIFF(hour, 0, CheckedUtc), 0)
            """,
            new { since = Utc.From(sinceUtc) }, ct)).ConfigureAwait(false);
        return rows.Select(r => new ProbeHour(r.TargetKey, Utc.To(r.HourUtc), r.Samples, r.OnlineSamples)).ToList();
    }

    public async Task<DateTimeOffset?> FirstSampleAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return Utc.To(await conn.ExecuteScalarAsync<DateTime?>(Cmd("SELECT MIN(CheckedUtc) FROM dbo.ServerProbe", null, ct)).ConfigureAwait(false));
    }

    public async Task<int> PruneAsync(DateTimeOffset beforeUtc, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(Cmd("DELETE TOP (50000) FROM dbo.ServerProbe WHERE CheckedUtc < @before", new { before = Utc.From(beforeUtc) }, ct)).ConfigureAwait(false);
    }

    private sealed class HourRow
    {
        public string TargetKey { get; init; } = "";
        public DateTime HourUtc { get; init; }
        public int Samples { get; init; }
        public int OnlineSamples { get; init; }
    }
}

internal sealed class ServerNoticeStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), IServerNoticeStore
{
    private const string Columns = "Id, Kind, Title, Body, StartsUtc, EndsUtc, Status, CreatedUtc, UpdatedUtc, CreatedBy, UpdatedBy";

    public async Task<(IReadOnlyList<ServerNotice> Items, int Total)> ListAsync(int skip, int take, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT COUNT(*) FROM dbo.ServerNotice;
            SELECT {Columns} FROM dbo.ServerNotice ORDER BY StartsUtc DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new { skip, take }, ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<Row>().ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<ServerNotice?> GetAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<Row>(Cmd($"SELECT {Columns} FROM dbo.ServerNotice WHERE Id = @id", new { id }, ct)).ConfigureAwait(false);
        return r?.ToModel();
    }

    public async Task<int> CreateAsync(NoticeData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(Cmd(
            """
            INSERT INTO dbo.ServerNotice (Kind, Title, Body, StartsUtc, EndsUtc, Status, CreatedBy, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES (@Kind, @Title, @Body, @StartsUtc, @EndsUtc, @Status, @by, @by)
            """,
            Args(d, by, null), ct)).ConfigureAwait(false);
    }

    public async Task UpdateAsync(int id, NoticeData d, string by, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            """
            UPDATE dbo.ServerNotice
               SET Kind = @Kind, Title = @Title, Body = @Body, StartsUtc = @StartsUtc, EndsUtc = @EndsUtc, Status = @Status,
                   UpdatedBy = @by, UpdatedUtc = SYSUTCDATETIME()
             WHERE Id = @id
            """,
            Args(d, by, id), ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerNotice>> ListCurrentAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(Cmd(
            $"""
            SELECT TOP (20) {Columns} FROM dbo.ServerNotice
            WHERE Status = 'published' AND (EndsUtc IS NULL OR EndsUtc > @now)
            ORDER BY StartsUtc
            """,
            new { now = Utc.From(now) }, ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    private static object Args(NoticeData d, string by, int? id) => new
    {
        id,
        Kind = Ansi(d.Kind, 16),
        d.Title,
        d.Body,
        StartsUtc = Utc.From(d.StartsUtc),
        EndsUtc = d.EndsUtc is null ? (DateTime?)null : Utc.From(d.EndsUtc.Value),
        Status = Ansi(d.Status, 16),
        by = Ansi(by, 32),
    };

    private sealed class Row
    {
        public int Id { get; init; }
        public string Kind { get; init; } = "";
        public string Title { get; init; } = "";
        public string Body { get; init; } = "";
        public DateTime StartsUtc { get; init; }
        public DateTime? EndsUtc { get; init; }
        public string Status { get; init; } = "";
        public DateTime CreatedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; init; }

        public ServerNotice ToModel() => new(Id, Kind, Title, Body, Utc.To(StartsUtc), Utc.To(EndsUtc), Status, Utc.To(CreatedUtc), Utc.To(UpdatedUtc), CreatedBy, UpdatedBy);
    }
}
