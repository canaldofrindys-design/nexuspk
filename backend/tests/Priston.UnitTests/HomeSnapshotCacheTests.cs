using Priston.Application.Home;
using Priston.Application.Server;

namespace Priston.UnitTests;

public sealed class HomeSnapshotCacheTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 25, 13, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static HomeSnapshot Snap(int n) => new(
        new HomeServerInfo($"S{n}", string.Empty, new Dictionary<string, string>(), []),
        new ServerStatus(ServerAvailability.Unknown, null, null),
        [], true, [], [], [], []);

    [Fact]
    public async Task First_call_waits_for_the_home_and_the_next_ones_reuse_it()
    {
        var clock = new Clock();
        var builds = 0;
        var cache = new HomeSnapshotCache(_ => Task.FromResult(Snap(++builds)), clock);

        var first = await cache.GetAsync(CancellationToken.None);
        var second = await cache.GetAsync(CancellationToken.None);

        Assert.Equal("S1", first.Server.Name);
        Assert.Same(first, second);
        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task An_old_home_is_answered_right_away_while_a_new_one_is_built()
    {
        var clock = new Clock();
        var builds = 0;
        var slow = new TaskCompletionSource<HomeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new HomeSnapshotCache(_ => ++builds == 1 ? Task.FromResult(Snap(1)) : slow.Task, clock);
        await cache.GetAsync(CancellationToken.None);

        clock.Now += HomeSnapshotCache.FreshFor + TimeSpan.FromSeconds(1);
        var stale = await cache.GetAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var again = await cache.GetAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("S1", stale.Server.Name);
        Assert.Equal("S1", again.Server.Name);

        slow.SetResult(Snap(2));
        var refreshed = await cache.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, builds);
        Assert.Equal("S2", refreshed.Server.Name);
        Assert.Equal("S2", (await cache.GetAsync(CancellationToken.None)).Server.Name);
    }

    [Fact]
    public async Task A_failed_build_is_tried_again_on_the_next_call()
    {
        var clock = new Clock();
        var builds = 0;
        var cache = new HomeSnapshotCache(_ => ++builds == 1 ? throw new InvalidOperationException("fora") : Task.FromResult(Snap(builds)), clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(CancellationToken.None));
        var home = await cache.GetAsync(CancellationToken.None);

        Assert.Equal("S2", home.Server.Name);
    }
}
