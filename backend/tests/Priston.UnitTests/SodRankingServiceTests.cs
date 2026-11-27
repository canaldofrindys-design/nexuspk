using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Characters;
using Priston.Domain.Time;
using Priston.GameDatabaseIntegration.Characters;

namespace Priston.UnitTests;

public class SodRankingServiceTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Levels(params CharacterLevelRow[] rows) : ILevelRankingReader
    {
        public Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CharacterLevelRow>>(rows);
    }

    private sealed class Pvp(params PvpRow[] rows) : IPvpRankingReader
    {
        public Task<IReadOnlyList<PvpRow>> ReadAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PvpRow>>(rows);
    }

    private sealed class Clans(params ClanRow[] rows) : IClanRankingReader
    {
        public Task<IReadOnlyList<ClanRow>> ReadAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ClanRow>>(rows);
    }

    private sealed class Members : ICharacterClanReader
    {
        public Task<IReadOnlyDictionary<string, string>> ReadAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Lupus"] = "Lobos" });
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

    private sealed class Sod(Dictionary<DateOnly, SodPlayerRow[]> months, bool fail = false) : ISodRankingReader
    {
        public Task<IReadOnlyList<SodPlayerRow>?> ReadMonthAsync(DateOnly month, CancellationToken ct) =>
            fail ? throw new InvalidOperationException("sem permissão")
                : Task.FromResult<IReadOnlyList<SodPlayerRow>?>(months.TryGetValue(month, out var rows) ? rows : null);

        public Task<IReadOnlyList<DateOnly>> ReadMonthsAsync(CancellationToken ct) =>
            fail ? throw new InvalidOperationException("sem permissão")
                : Task.FromResult<IReadOnlyList<DateOnly>>(months.Keys.OrderDescending().ToList());
    }

    // 01/12/2026 01:00 UTC = 30/11/2026 22:00 em Brasília: o mês do SoD ainda é novembro.
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 1, 0, 0, TimeSpan.Zero);

    private static (SodRankingService Svc, MemoryCache Cache) Create(ISodRankingReader sod)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var levels = new RankingService(new Levels(new("Lupus", 9, 160), new("Aria", 5, 150), new("Brutus", 1, 170)), cache);
        var clock = new Clock(Now);
        var queries = new RankingQueryService(
            levels,
            new Pvp(
                new("Lupus", 9, 100, 0, 0, 0, false, 0, 40),
                new("lupus", 9, 10, 0, 0, 0, false, 0, 90),
                new("Aria", 5, 50, 0, 0, 0, false, 0, 0),
                new("Brutus", 1, 20, 0, 0, 0, false, 0, 40)),
            new Clans(
                new("Lobos", 12, 900, 3, 1, null, 7, "Lupus"),
                new("Guarda", 20, 900, 2, 2, null, 8, "Aria"),
                new("Sem SoD", 30, 0, 0, 0, null),
                new("Corvos", 5, 300, 0, 0, null),
                new("Ursos", 5, 100, 0, 0, null)),
            new Members(),
            new NoSnapshots(),
            new RankingPeriods(),
            cache,
            clock);
        return (new SodRankingService(sod, queries, levels, cache, clock), cache);
    }

    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly DateOnly October = new(2026, 10, 1);

    [Fact]
    public async Task Monthly_ranking_uses_the_current_month_in_brasilia_with_clan_and_class_filter()
    {
        var (svc, cache) = Create(new Sod(new()
        {
            [November] = [new("Lupus", 9, 160, 5000), new("Aria", 5, 150, 3000)],
            [October] = [new("Brutus", 1, 170, 9000)],
        }));
        using var _ = cache;

        var page = await svc.GetMonthlyAsync(null, null, 1, 50, default);
        var archers = await svc.GetMonthlyAsync(null, 5, 1, 50, default);
        var october = await svc.GetMonthlyAsync(October, null, 1, 50, default);

        Assert.True(page.Available);
        Assert.Equal(November, page.PeriodStart);
        Assert.Equal(["Lupus", "Aria"], page.Items.Select(i => i.Name));
        Assert.Equal("Lobos", page.Items[0].Clan);
        Assert.Equal(5000, page.Items[0].Points);
        Assert.Equal(["Aria"], archers.Items.Select(i => i.Name));
        Assert.Equal(1, archers.Items[0].Position);
        Assert.Equal(["Brutus"], october.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Month_without_table_or_without_access_is_unavailable_instead_of_empty()
    {
        var (missing, c1) = Create(new Sod(new() { [October] = [new("Brutus", 1, 170, 9000)] }));
        var (failing, c2) = Create(new Sod([], fail: true));
        using var _1 = c1;
        using var _2 = c2;

        var noTable = await missing.GetMonthlyAsync(null, null, 1, 50, default);
        var noAccess = await failing.GetMonthlyAsync(null, null, 1, 50, default);
        var months = await failing.GetMonthsAsync(default);

        Assert.False(noTable.Available);
        Assert.Empty(noTable.Items);
        Assert.False(noAccess.Available);
        Assert.False(months.Available);
        Assert.Equal(November, months.Current);
    }

    [Fact]
    public async Task Months_list_is_newest_first_and_never_in_the_future()
    {
        var (svc, cache) = Create(new Sod(new()
        {
            [October] = [],
            [November] = [],
            [new DateOnly(2026, 12, 1)] = [],
        }));
        using var _ = cache;

        var months = await svc.GetMonthsAsync(default);

        Assert.True(months.Available);
        Assert.Equal([November, October], months.Months);
    }

    [Fact]
    public async Task Weekly_ranking_reads_the_game_counter_once_per_character_with_level()
    {
        var (svc, cache) = Create(new Sod([]));
        using var _ = cache;

        var page = await svc.GetWeeklyAsync(null, 1, 50, default);

        // Lupus tem duas linhas em BattlePoints: vale o maior SoD da semana (90). Aria não pontuou.
        Assert.Equal(["Lupus", "Brutus"], page.Items.Select(i => i.Name));
        Assert.Equal(90, page.Items[0].Points);
        Assert.Equal(160, page.Items[0].Level);
        Assert.Equal("Lobos", page.Items[0].Clan);
        Assert.Null(page.PeriodStart);
    }

    [Fact]
    public async Task Clan_ranking_skips_clans_without_points_and_crowns_the_top_three()
    {
        var (svc, cache) = Create(new Sod([]));
        using var _ = cache;

        var page = await svc.GetClansAsync(1, 50, default);

        Assert.Equal(["Guarda", "Lobos", "Corvos", "Ursos"], page.Items.Select(i => i.Name));
        Assert.Equal([1, 2, 3, 0], page.Items.Select(i => i.Crown));
        Assert.Equal("Lupus", page.Items[1].Leader);
        Assert.Equal(7, page.Items[1].IconId);
    }

    [Theory]
    [InlineData("SOD2Record2611", 2026, 11)]
    [InlineData("SOD2Record0901", 2009, 1)]
    public void Table_name_gives_the_month(string table, int year, int month) =>
        Assert.Equal(new DateOnly(year, month, 1), SodRankingReader.ParseMonth(table));

    [Theory]
    [InlineData("SOD2Record2613")]
    [InlineData("SOD2Record261")]
    [InlineData("SOD2RecordAB11")]
    [InlineData("Outra2611")]
    public void Table_name_out_of_pattern_is_ignored(string table) => Assert.Null(SodRankingReader.ParseMonth(table));
}
