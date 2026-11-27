using Priston.Application.Accounts;
using Priston.Application.Player;

namespace Priston.UnitTests;

public class GameDisconnectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 19, 0, 0, TimeSpan.Zero);
    private static readonly ClientInfo Client = new("203.0.113.9", "teste");

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Value { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Value;
    }

    private sealed class MemoryStore : IGameDisconnectStore
    {
        public DisconnectRuntime? Runtime { get; set; } = new(GameDisconnectService.ProtocolVersion, Now.AddSeconds(-10));
        public List<(long Id, string Login, DateTimeOffset At, string Status)> Rows { get; } = [];

        public Task<DisconnectRuntime?> RuntimeAsync(CancellationToken ct) => Task.FromResult(Runtime);

        public Task<DisconnectRequestRow?> LatestAsync(string gameLogin, CancellationToken ct) => Task.FromResult(
            Rows.Where(r => string.Equals(r.Login, gameLogin, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.At).ThenByDescending(r => r.Id)
                .Select(r => new DisconnectRequestRow(r.Id, r.At, r.Status, null, null))
                .FirstOrDefault());

        public Task<int> CountSinceAsync(string gameLogin, DateTimeOffset sinceUtc, CancellationToken ct) =>
            Task.FromResult(Rows.Count(r => string.Equals(r.Login, gameLogin, StringComparison.OrdinalIgnoreCase) && r.At >= sinceUtc));

        public Task<long?> TryCreateAsync(string gameLogin, DateTimeOffset nowUtc, DateTimeOffset pendingSinceUtc, string? ipAddress, CancellationToken ct)
        {
            if (Rows.Any(r => string.Equals(r.Login, gameLogin, StringComparison.OrdinalIgnoreCase) && r.Status == DisconnectStatus.Pending && r.At > pendingSinceUtc))
            {
                return Task.FromResult<long?>(null);
            }

            var id = Rows.Count + 1L;
            Rows.Add((id, gameLogin, nowUtc, DisconnectStatus.Pending));
            return Task.FromResult<long?>(id);
        }

        public void Answer(long id, string status)
        {
            var i = Rows.FindIndex(r => r.Id == id);
            Rows[i] = Rows[i] with { Status = status };
        }
    }

    private static (GameDisconnectService Service, MemoryStore Store, MemoryAudit Audit, FixedTime Clock) Create()
    {
        var store = new MemoryStore();
        var audit = new MemoryAudit();
        var clock = new FixedTime(Now);
        return (new GameDisconnectService(store, audit, clock), store, audit, clock);
    }

    [Fact]
    public async Task Request_is_queued_and_audited()
    {
        var (service, store, audit, _) = Create();

        var result = await service.RequestAsync("jogador", Client, default);

        Assert.Equal(DisconnectOutcome.Ok, result.Outcome);
        Assert.Single(store.Rows);
        Assert.Equal(DisconnectStatus.Pending, result.State.Last!.Status);
        Assert.False(result.State.Available);
        Assert.Contains(audit.Rows, r => r.Action == "player.game.disconnect-requested" && r.Actor == "jogador");
    }

    [Fact]
    public async Task Second_request_while_pending_is_refused()
    {
        var (service, store, _, _) = Create();
        await service.RequestAsync("jogador", Client, default);

        var again = await service.RequestAsync("jogador", Client, default);

        Assert.Equal(DisconnectOutcome.AlreadyPending, again.Outcome);
        Assert.Single(store.Rows);
    }

    [Fact]
    public async Task Server_without_signal_refuses_and_writes_nothing()
    {
        var (service, store, _, _) = Create();
        store.Runtime = new(GameDisconnectService.ProtocolVersion, Now.AddMinutes(-5));

        var result = await service.RequestAsync("jogador", Client, default);

        Assert.Equal(DisconnectOutcome.Unavailable, result.Outcome);
        Assert.Empty(store.Rows);
        Assert.Equal(GameDisconnectService.ServerOffMessage, result.State.UnavailableReason);
    }

    [Theory]
    [InlineData(0, null, false)]    // linha semeada pela migração: servidor ainda sem a função
    [InlineData(0, 5, false)]       // sinal de um servidor com protocolo antigo
    [InlineData(1, 5, true)]
    [InlineData(1, 119, true)]
    [InlineData(1, 120, false)]     // dois minutos sem sinal: desligado
    public void Server_answering_needs_protocol_and_recent_signal(int protocol, int? secondsAgo, bool expected)
    {
        var runtime = new DisconnectRuntime(protocol, secondsAgo is null ? null : Now.AddSeconds(-secondsAgo.Value));

        Assert.Equal(expected, GameDisconnectService.ServerAnswering(runtime, Now));
        Assert.False(GameDisconnectService.ServerAnswering(null, Now));
    }

    [Fact]
    public async Task Pending_without_answer_expires_and_allows_a_new_request()
    {
        var (service, store, _, clock) = Create();
        await service.RequestAsync("jogador", Client, default);

        clock.Value = Now.AddMinutes(3);
        store.Runtime = new(GameDisconnectService.ProtocolVersion, clock.Value.AddSeconds(-5));
        var state = await service.GetAsync("jogador", default);
        Assert.Equal(DisconnectStatus.Expired, state.Last!.Status);
        Assert.True(state.Available);

        var again = await service.RequestAsync("jogador", Client, default);
        Assert.Equal(DisconnectOutcome.Ok, again.Outcome);
        Assert.Equal(2, store.Rows.Count);
    }

    [Fact]
    public async Task Answered_request_shows_server_status()
    {
        var (service, store, _, _) = Create();
        await service.RequestAsync("jogador", Client, default);
        store.Answer(1, DisconnectStatus.NotOnline);

        var state = await service.GetAsync("jogador", default);

        Assert.Equal(DisconnectStatus.NotOnline, state.Last!.Status);
        Assert.True(state.Available);
    }

    [Fact]
    public async Task Hourly_limit_blocks_after_max_requests()
    {
        var (service, store, _, clock) = Create();
        for (var i = 0; i < GameDisconnectService.MaxPerHour; i++)
        {
            clock.Value = Now.AddMinutes(i * 3);
            store.Runtime = new(GameDisconnectService.ProtocolVersion, clock.Value);
            Assert.Equal(DisconnectOutcome.Ok, (await service.RequestAsync("jogador", Client, default)).Outcome);
            store.Answer(i + 1, DisconnectStatus.Done);
        }

        clock.Value = Now.AddMinutes(GameDisconnectService.MaxPerHour * 3);
        store.Runtime = new(GameDisconnectService.ProtocolVersion, clock.Value);
        var blocked = await service.RequestAsync("jogador", Client, default);

        Assert.Equal(DisconnectOutcome.TooMany, blocked.Outcome);
        Assert.NotNull(blocked.State.NextAllowedUtc);
        Assert.Equal(GameDisconnectService.MaxPerHour, store.Rows.Count);
    }

    [Fact]
    public async Task Accounts_are_independent()
    {
        var (service, store, _, _) = Create();
        await service.RequestAsync("um", Client, default);

        var other = await service.RequestAsync("dois", Client, default);

        Assert.Equal(DisconnectOutcome.Ok, other.Outcome);
        Assert.Equal(2, store.Rows.Count);
    }
}
