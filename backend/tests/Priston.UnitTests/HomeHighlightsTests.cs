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
        public Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CharacterLevelRow>>([new("ReiDoNorte", 6, 180), new("Lupus", 9, 160)]);
    }

    private sealed class Sod(SodPlayerRow? top, bool fail = false) : ISodRankingReader
    {
        public DateOnly? Month { get; private set; }

        public Task<SodPlayerRow?> ReadTopAsync(DateOnly month, CancellationToken ct)
        {
            Month = month;
            return fail ? throw new InvalidOperationException("sem permissão") : Task.FromResult(top);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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
        public Task<IReadOnlyDictionary<string, string>> ReadAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Fera"] = "Lobos" });
    }

    private sealed class NoSnapshots : ILevelSnapshotStore
    {
        public Task<bool> ExistsAsync(DateOnly day, CancellationToken ct) => Task.FromResult(false);

        public Task SaveAsync(DateOnly day, DateTimeOffset takenUtc, IReadOnlyList<CharacterLevelRow> rows, CancellationToken ct) => Task.CompletedTask;

        public Task<(DateOnly Date, IReadOnlyDictionary<string, int> Levels)?> FirstOnOrAfterAsync(DateOnly from, CancellationToken ct) =>
            Task.FromResult<(DateOnly, IReadOnlyDictionary<string, int>)?>(null);

        public Task<DateOnly?> FirstDateAsync(CancellationToken ct) => Task.FromResult<DateOnly?>(null);

        public Task<IReadOnlyList<LevelSnapshotPoint>> ReadSinceAsync(DateOnly from, CancellationToken ct) => Task.FromResult<IReadOnlyList<LevelSnapshotPoint>>([]);

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

    private static HomeService Create(MemoryCache cache, ContentFixture content, IPvpRankingReader pvp, IBlessCastleOwnerReader castle, params ClanRow[] clans) =>
        Create(cache, content, pvp, castle, null, clans);

    private static HomeService Create(MemoryCache cache, ContentFixture content, IPvpRankingReader pvp, IBlessCastleOwnerReader castle, ISodRankingReader? sod, params ClanRow[] clans)
    {
        var ranking = new RankingService(new NoLevels(), cache);
        var queries = new RankingQueryService(ranking, pvp, new Clans(clans), new NoMembers(), new NoSnapshots(), new RankingPeriods(), cache, TimeProvider.System);
        return new HomeService(ranking, new UnknownStatus(), content.Service(), Options.Create(new ServerPresentationOptions()), null, queries, castle, sod,
            new FixedClock(new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero)));
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
        Assert.Equal("Guarda", snapshot.BlessCastle!.Clan!.Name);
        Assert.Equal(12, snapshot.BlessCastle.Clan.Members);
        Assert.Equal(2, snapshot.BlessCastle.Clan.SodPosition);
        Assert.Equal("Lobos", snapshot.SodLeader!.Clan!.Name);
        Assert.Equal(800, snapshot.SodLeader.Clan.Points);
        Assert.Equal(1, snapshot.SodLeader.Clan.SodPosition);
    }

    [Fact]
    public async Task Home_shows_game_patents_and_biggest_clan()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var pvp = new Pvp(
            new("Alfa", 1, 500, 0, 10, 2, false, 250),
            new("Bravo", 7, 900, 0, 30, 9, true, 120),
            new("Zero", 2, 0, 0, 0, 0, false, 40));
        var home = Create(cache, content, pvp, new Castle(null),
            new ClanRow("Guarda", 12, 300, 5, 1, null, 42),
            new ClanRow("Lobos", 20, 800, 2, 2, null, 7));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.PvpTiers!.Total);
        Assert.Equal("prata-de-elite", snapshot.PvpTiers.Tiers[0].Key);
        Assert.Equal("Alfa", snapshot.PvpTiers.Tiers.Single(t => t.Key == "ouro-mestre").Top!.Name);
        var king = snapshot.PvpTiers.Tiers[^1];
        Assert.Equal("rei-pvp", king.Key);
        Assert.Equal("Bravo", king.Top!.Name);
        Assert.Equal("Lobos", snapshot.BiggestClan!.Clan!.Name);
        Assert.Equal(20, snapshot.BiggestClan.Clan.Members);
    }

    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(99, false, 0)]
    [InlineData(100, false, 1)]
    [InlineData(199, false, 1)]
    [InlineData(250, false, 2)]
    [InlineData(699, false, 6)]
    [InlineData(700, false, 7)]
    [InlineData(500000, false, 7)]
    [InlineData(10, true, 8)]
    public void Patent_follows_the_game_client_rule(int points, bool king, int level)
    {
        Assert.Equal(level, PvpPatents.LevelOf(points, king));
        Assert.Equal(level == 0 ? (int?)null : level, PvpPatents.Of(points, king)?.Level);
    }

    [Fact]
    public void Patent_ladder_groups_by_points_and_keeps_the_king_apart()
    {
        PvpEntry P(string name, int points, bool king = false) => new(0, name, 1, "Lutador", 0, 0, 0, 0, king, points);
        var ladder = RankingQueryService.BuildTiers([P("A", 150), P("B", 180), P("C", 720), P("D", 99), P("Rei", 900, true)]);

        var byKey = ladder.Tiers.ToDictionary(t => t.Key);
        Assert.Equal(8, ladder.Tiers.Count);
        Assert.Equal(4, ladder.Total);
        Assert.Equal(2, byKey["prata-de-elite"].Players);
        Assert.Equal("B", byKey["prata-de-elite"].Top!.Name);
        Assert.Equal(1, byKey["mestre-supremo"].Players);
        Assert.Equal("Rei", byKey["rei-pvp"].Top!.Name);
        Assert.Equal(0, byKey["ouro-mestre"].Players);
        Assert.Null(byKey["ouro-mestre"].Top);

        var empty = RankingQueryService.BuildTiers([]);
        Assert.All(empty.Tiers, t => Assert.Equal(0, t.Players));
    }

    [Fact]
    public async Task Clan_details_bring_leader_character_emblem_and_month_top_sod_player()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var sod = new Sod(new SodPlayerRow("Fera", 9, 170, 5400));
        var home = Create(cache, content, new Pvp(), new Castle(42), sod,
            new ClanRow("Guarda", 12, 300, 5, 1, new DateTime(2025, 3, 10, 12, 0, 0), 42, " ReiDoNorte "),
            new ClanRow("Lobos", 20, 800, 2, 2, null, 7, "Sumido"));

        var snapshot = await home.GetAsync(CancellationToken.None);

        var castle = snapshot.BlessCastle!.Clan!;
        Assert.Equal(42, castle.IconId);
        Assert.Equal(new HomeCharacter("ReiDoNorte", 6, "Cavaleiro", 180), castle.Leader);
        Assert.Equal((5, 1), (castle.Wins, castle.Losses));
        Assert.Equal(new DateTimeOffset(2025, 3, 10, 15, 0, 0, TimeSpan.Zero), castle.CreatedUtc);
        Assert.Equal(new HomeCharacter("Sumido", 0, string.Empty, null), snapshot.SodLeader!.Clan!.Leader);
        // 01/12 01:00 UTC ainda é 30/11 em Brasília: tabela de novembro.
        Assert.Equal(new DateOnly(2026, 11, 1), sod.Month);
        Assert.Equal(new HomeSodPlayer("Fera", 9, "Assassina", 170, 5400, "Lobos"), snapshot.SodLeader.TopPlayer);
    }

    [Fact]
    public async Task Sod_top_player_failure_keeps_the_clan_leader()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var home = Create(cache, content, new Pvp(), new Castle(null), new Sod(null, fail: true), new ClanRow("Lobos", 20, 800, 2, 2, null, 7));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.Equal("Lobos", snapshot.SodLeader!.Clan!.Name);
        Assert.Null(snapshot.SodLeader.TopPlayer);
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
