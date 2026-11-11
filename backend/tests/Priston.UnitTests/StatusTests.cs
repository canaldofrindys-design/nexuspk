using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Server;
using Priston.Infrastructure.GameServer;

namespace Priston.UnitTests;

internal sealed class FakeProbe(params TargetStatus[] targets) : IServerProbe
{
    public IReadOnlyList<ProbeTarget> Targets => targets.Select(t => new ProbeTarget(t.Key, t.Name)).ToList();

    public Task<ServerStatus> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ServerStatus(IServerProbe.Combine(targets), ContentFixture.Now, null));

    public Task<ServerSnapshot> GetSnapshotAsync(CancellationToken ct) => ProbeNowAsync(ct);

    public Task<ServerSnapshot> ProbeNowAsync(CancellationToken ct) =>
        Task.FromResult(new ServerSnapshot(IServerProbe.Combine(targets), targets.Length == 0 ? null : ContentFixture.Now, targets));
}

internal sealed class MemoryProbeHistory : IServerProbeStore
{
    public List<ProbeHour> Hours { get; } = [];

    public Task RecordAsync(ServerSnapshot snapshot, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<ProbeHour>> HourlyAsync(DateTimeOffset sinceUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProbeHour>>(Hours.Where(h => h.HourUtc >= sinceUtc.AddHours(-1)).ToList());

    public Task<DateTimeOffset?> FirstSampleAsync(CancellationToken ct) =>
        Task.FromResult(Hours.Count == 0 ? (DateTimeOffset?)null : Hours.Min(h => h.HourUtc));

    public Task<int> PruneAsync(DateTimeOffset beforeUtc, CancellationToken ct) => Task.FromResult(0);
}

internal sealed class MemoryNotices : IServerNoticeStore
{
    public List<ServerNotice> Rows { get; } = [];

    public Task<(IReadOnlyList<ServerNotice> Items, int Total)> ListAsync(int skip, int take, CancellationToken ct) =>
        Task.FromResult<(IReadOnlyList<ServerNotice>, int)>((Rows.ToList(), Rows.Count));

    public Task<ServerNotice?> GetAsync(int id, CancellationToken ct) => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));

    public Task<int> CreateAsync(NoticeData d, string by, CancellationToken ct)
    {
        var id = Rows.Count + 1;
        Rows.Add(new ServerNotice(id, d.Kind, d.Title, d.Body, d.StartsUtc, d.EndsUtc, d.Status, ContentFixture.Now, ContentFixture.Now, by, by));
        return Task.FromResult(id);
    }

    public Task UpdateAsync(int id, NoticeData d, string by, CancellationToken ct)
    {
        var i = Rows.FindIndex(r => r.Id == id);
        Rows[i] = Rows[i] with { Kind = d.Kind, Title = d.Title, Body = d.Body, StartsUtc = d.StartsUtc, EndsUtc = d.EndsUtc, Status = d.Status, UpdatedBy = by };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ServerNotice>> ListCurrentAsync(DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ServerNotice>>(Rows.Where(r => r.Status == "published" && (r.EndsUtc is null || r.EndsUtc > now)).ToList());
}

public sealed class StatusPageTests : IDisposable
{
    private static readonly DateTimeOffset Now = ContentFixture.Now; // 15:00 UTC
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly MemoryProbeHistory _history = new();
    private readonly MemoryNotices _notices = new();

    public void Dispose() => _cache.Dispose();

    private StatusPageService Service(params TargetStatus[] targets) => new(new FakeProbe(targets), _history, _notices, _cache, new FixedClock(Now));

    [Fact]
    public async Task No_history_means_null_uptime_not_zero_or_hundred()
    {
        var r = await Service(new TargetStatus("login", "Login", true, 3)).GetAsync(default);
        var t = Assert.Single(r.Targets);
        Assert.Null(t.Uptime24h);
        Assert.Null(t.Uptime7d);
        Assert.Equal(24, t.Last24h.Count);
        Assert.All(t.Last24h, c => Assert.Null(c.Online));
        Assert.Null(r.HistorySinceUtc);
        Assert.Null(r.PlayersOnline);
    }

    [Fact]
    public async Task Uptime_is_computed_from_recorded_samples_only()
    {
        var hour = new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
        _history.Hours.Add(new ProbeHour("login", hour, 30, 30));
        _history.Hours.Add(new ProbeHour("login", hour.AddHours(-1), 60, 30));
        _history.Hours.Add(new ProbeHour("login", hour.AddDays(-3), 60, 0));
        var r = await Service(new TargetStatus("login", "Login", true, 3), new TargetStatus("canal-1", "Canal 1", false, null)).GetAsync(default);

        Assert.Equal(ServerAvailability.Partial, r.Availability);
        var login = r.Targets[0];
        Assert.Equal(60.0 / 90, login.Uptime24h!.Value, 3);
        Assert.Equal(60.0 / 150, login.Uptime7d!.Value, 3);
        Assert.Equal(1.0, login.Last24h[^1].Online);
        Assert.Equal(0.5, login.Last24h[^2].Online);
        Assert.Null(login.Last24h[0].Online);
        Assert.Null(r.Targets[1].Uptime24h);
        Assert.Equal(hour.AddDays(-3), r.HistorySinceUtc);
    }

    [Fact]
    public async Task Active_maintenance_and_upcoming_notices()
    {
        _notices.Rows.Add(new ServerNotice(1, "manutencao", "Manutenção semanal", "Reinício.", Now.AddMinutes(-10), Now.AddHours(1), "published", Now, Now, "ana", "ana"));
        _notices.Rows.Add(new ServerNotice(2, "aviso", "Evento amanhã", "Texto.", Now.AddDays(1), null, "published", Now, Now, "ana", "ana"));
        _notices.Rows.Add(new ServerNotice(3, "aviso", "Rascunho", "Texto.", Now.AddDays(-1), null, "draft", Now, Now, "ana", "ana"));
        _notices.Rows.Add(new ServerNotice(4, "manutencao", "Antiga", "Texto.", Now.AddDays(-2), Now.AddDays(-2).AddHours(1), "published", Now, Now, "ana", "ana"));
        var r = await Service(new TargetStatus("login", "Login", false, null)).GetAsync(default);

        Assert.True(r.MaintenanceNow);
        Assert.Equal([1, 2], r.Notices.Select(n => n.Id));
        Assert.True(r.Notices[0].Active);
        Assert.False(r.Notices[1].Active);
    }

    [Fact]
    public async Task Notice_validation_and_audit()
    {
        var audit = new MemoryAudit();
        var admin = new ServerNoticeAdminService(_notices, Service(), audit);
        var me = new StaffPrincipal(1, "ana", "Ana", StaffRoles.Moderator, false, Now.AddHours(1));
        var client = new ClientInfo("198.51.100.7", null);

        var bad = await admin.SaveAsync(me, null, new NoticeInput("manutencao", "", "", "2026-11-14T03:00", null, "published"), client, default);
        Assert.Equal(StaffAuthStatus.ValidationFailed, bad.Status);
        Assert.Contains("title", bad.Errors!.Keys);
        Assert.Contains("endsLocal", bad.Errors.Keys);

        var ok = await admin.SaveAsync(me, null, new NoticeInput("manutencao", "Manutenção semanal", "Servidor reinicia.", "2026-11-14T03:00", "2026-11-14T05:00", "published"), client, default);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 6, 0, 0, TimeSpan.Zero), _notices.Rows[0].StartsUtc); // 03:00 em Brasília
        Assert.Equal("notice.created", audit.Rows[^1].Action);
    }
}

public class MultiTargetProbeTests
{
    [Fact]
    public async Task Login_up_and_channel_down_is_partial()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var closedPort = ((IPEndPoint)closed.LocalEndpoint).Port;
        closed.Stop();
        try
        {
            var options = new GameServerOptions { TimeoutMilliseconds = 1000 };
            options.Targets.Add(new GameServerTarget { Key = "Login", Name = "Login", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port });
            options.Targets.Add(new GameServerTarget { Key = "canal-1", Name = "Canal 1", Host = "127.0.0.1", Port = closedPort });
            options.Targets.Add(new GameServerTarget { Key = "sem-porta", Name = "Ignorado", Host = "127.0.0.1", Port = 0 });
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var probe = new GameServerProbe(Options.Create(options), cache, TimeProvider.System);

            Assert.Equal(["login", "canal-1"], probe.Targets.Select(t => t.Key));
            var s = await probe.ProbeNowAsync(default);
            Assert.Equal(ServerAvailability.Partial, s.Availability);
            Assert.True(s.Targets[0].Online);
            Assert.NotNull(s.Targets[0].LatencyMs);
            Assert.False(s.Targets[1].Online);
            Assert.Null(s.Targets[1].LatencyMs);
            Assert.Equal(ServerAvailability.Partial, (await probe.GetAsync(default)).Availability);
        }
        finally
        {
            listener.Stop();
        }
    }
}
