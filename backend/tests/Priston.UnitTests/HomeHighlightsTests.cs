using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Home;
using Priston.Application.Server;
using Priston.Domain.Time;
using Priston.Infrastructure.GameServer;

namespace Priston.UnitTests;

public class HomeHighlightsTests
{
    private sealed class NoLevels : ILevelRankingReader
    {
        public Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CharacterLevelRow>>([]);
    }

    private sealed class Pvp(params PvpRow[] rows) : IPvpRankingReader
    {
        public Task<IReadOnlyList<PvpRow>> ReadAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PvpRow>>(rows);
    }

    private sealed class FailingPvp : IPvpRankingReader
    {
        public Task<IReadOnlyList<PvpRow>> ReadAllAsync(CancellationToken ct) => throw new InvalidOperationException("sem banco");
    }

    private sealed class Clans(params ClanRow[] rows) : IClanRankingReader
    {
        public Task<IReadOnlyList<ClanRow>> ReadAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ClanRow>>(rows);
    }

    private sealed class NoMembers : ICharacterClanReader
    {
        public Task<IReadOnlyDictionary<string, string>> ReadAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    private sealed class NoSnapshots : ILevelSnapshotStore
    {
        public Task<bool> ExistsAsync(DateOnly day, CancellationToken ct) => Task.FromResult(false);

        public Task SaveAsync(DateOnly day, DateTimeOffset takenUtc, IReadOnlyList<CharacterLevelRow> rows, CancellationToken ct) => Task.CompletedTask;

        public Task<(DateOnly Date, IReadOnlyDictionary<string, int> Levels)?> FirstOnOrAfterAsync(DateOnly from, CancellationToken ct) =>
            Task.FromResult<(DateOnly, IReadOnlyDictionary<string, int>)?>(null);

        public Task<DateOnly?> FirstDateAsync(CancellationToken ct) => Task.FromResult<DateOnly?>(null);

        public Task<int> DeleteBeforeAsync(DateOnly day, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class Castle(int? owner, bool fail = false) : IBlessCastleOwnerReader
    {
        public Task<int?> ReadOwnerIconIdAsync(CancellationToken ct) => fail ? throw new FileNotFoundException("sem arquivo") : Task.FromResult(owner);
    }

    private sealed class UnknownStatus : IServerStatusProvider
    {
        public Task<ServerStatus> GetAsync(CancellationToken cancellationToken) => Task.FromResult(new ServerStatus(ServerAvailability.Unknown, null, null));
    }

    private static HomeService Create(MemoryCache cache, ContentFixture content, IPvpRankingReader pvp, IBlessCastleOwnerReader castle, params ClanRow[] clans)
    {
        var ranking = new RankingService(new NoLevels(), cache);
        var queries = new RankingQueryService(ranking, pvp, new Clans(clans), new NoMembers(), new NoSnapshots(), new RankingPeriods(), cache, TimeProvider.System);
        return new HomeService(ranking, new UnknownStatus(), content.Service(), Options.Create(new ServerPresentationOptions()), null, queries, castle);
    }

    [Fact]
    public async Task Home_shows_top_battle_points_castle_owner_and_sod_leader()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var pvp = new Pvp(
            new("Alfa", 1, 500, 0, 10, 2, false),
            new("Bravo", 7, 900, 0, 30, 9, true),
            new("Zero", 2, 0, 0, 0, 0, false),
            new("C", 3, 10, 0, 1, 0, false), new("D", 3, 9, 0, 1, 0, false), new("E", 3, 8, 0, 1, 0, false), new("F", 3, 7, 0, 1, 0, false));
        var home = Create(cache, content, pvp, new Castle(42),
            new ClanRow("Guarda", 12, 300, 5, 1, null, 42),
            new ClanRow("Lobos", 20, 800, 2, 2, null, 7),
            new ClanRow("Vazio", 3, 0, 0, 0, null, 9));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.True(snapshot.BattlePointsAvailable);
        Assert.Equal(["Bravo", "Alfa", "C", "D", "E"], snapshot.TopBattlePoints!.Select(p => p.Name));
        Assert.Equal(new HomeClanLeader(true, new HomeClan("Guarda", 12, 300)), snapshot.BlessCastle);
        Assert.Equal(new HomeClanLeader(true, new HomeClan("Lobos", 20, 800)), snapshot.SodLeader);
    }

    [Fact]
    public async Task Castle_without_owner_and_sod_without_points_have_no_leader()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var home = Create(cache, content, new Pvp(), new Castle(null), new ClanRow("Vazio", 3, 0, 0, 0, null, 9));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.Equal(new HomeClanLeader(true, null), snapshot.BlessCastle);
        Assert.Equal(new HomeClanLeader(true, null), snapshot.SodLeader);
        Assert.True(snapshot.BattlePointsAvailable);
        Assert.Empty(snapshot.TopBattlePoints!);
    }

    [Fact]
    public async Task Failing_sources_are_marked_unavailable()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var home = Create(cache, content, new FailingPvp(), new Castle(null, fail: true), new ClanRow("Lobos", 20, 800, 2, 2, null, 7));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.False(snapshot.BattlePointsAvailable);
        Assert.False(snapshot.BlessCastle!.Available);
        Assert.True(snapshot.SodLeader!.Available);
    }

    [Theory]
    [InlineData(new byte[] { 1, 0, 0, 0, 42, 0, 0, 0 }, 42)]
    [InlineData(new byte[] { 1, 0, 0, 0, 0x00, 0xCA, 0x9A, 0x3B }, null)] // 1000000000: sem dono
    [InlineData(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, null)]
    public void Bless_castle_file_holds_owner_icon_at_byte_4(byte[] content, int? expected)
    {
        Assert.Equal(expected, BlessCastleFileReader.Parse(content));
    }

    [Fact]
    public void Bless_castle_file_too_short_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => BlessCastleFileReader.Parse(new byte[4]));
    }
}
