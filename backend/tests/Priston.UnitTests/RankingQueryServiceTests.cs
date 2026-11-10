using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Priston.Application.Characters;
using Priston.Domain.Time;

namespace Priston.UnitTests;

public class RankingQueryServiceTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Levels(params CharacterLevelRow[] rows) : ILevelRankingReader
    {
        public List<CharacterLevelRow> Rows { get; } = [.. rows];

        public Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CharacterLevelRow>>(Rows);
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
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Alfa"] = "Guarda" });
    }

    private sealed class Snapshots : ILevelSnapshotStore
    {
        public SortedDictionary<DateOnly, Dictionary<string, int>> Days { get; } = [];

        public Task<bool> ExistsAsync(DateOnly day, CancellationToken ct) => Task.FromResult(Days.ContainsKey(day));

        public Task SaveAsync(DateOnly day, DateTimeOffset takenUtc, IReadOnlyList<CharacterLevelRow> rows, CancellationToken ct)
        {
            Days[day] = rows.ToDictionary(r => r.Name, r => r.Level, StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }

        public Task<(DateOnly Date, IReadOnlyDictionary<string, int> Levels)?> FirstOnOrAfterAsync(DateOnly from, CancellationToken ct)
        {
            var hit = Days.Where(d => d.Key >= from).Select(d => ((DateOnly, IReadOnlyDictionary<string, int>)?)(d.Key, d.Value)).FirstOrDefault();
            return Task.FromResult(hit);
        }

        public Task<DateOnly?> FirstDateAsync(CancellationToken ct) => Task.FromResult(Days.Count == 0 ? (DateOnly?)null : Days.Keys.First());

        public Task<int> DeleteBeforeAsync(DateOnly day, CancellationToken ct)
        {
            var old = Days.Keys.Where(d => d < day).ToList();
            old.ForEach(d => Days.Remove(d));
            return Task.FromResult(old.Count);
        }
    }

    // Quarta-feira, 11/11/2026 15:00 em Brasília.
    private static readonly DateTimeOffset Wednesday = new(2026, 11, 11, 18, 0, 0, TimeSpan.Zero);

    private static (RankingQueryService Svc, Snapshots Snaps, Levels Levels, Clock Clock, MemoryCache Cache) Create()
    {
        var levels = new Levels(new("Alfa", 1, 120), new("ALFA", 1, 80), new("Bravo", 7, 110), new("Charlie", 3, 90), new("Novo", 4, 30));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var snaps = new Snapshots();
        var clock = new Clock(Wednesday);
        var svc = new RankingQueryService(
            new RankingService(levels, cache),
            new Pvp(new("Alfa", 1, 500, 40, 10, 2, true), new("alfa", 1, 100, 0, 1, 0, false), new("Bravo", 7, 900, 10, 30, 9, false), new("Zero", 2, 0, 0, 0, 0, false)),
            new Clans(new("Guarda", 12, 300, 5, 1, null), new("Lobos", 20, 300, 2, 2, null)),
            new Members(),
            snaps,
            new RankingPeriods(),
            cache,
            clock);
        return (svc, snaps, levels, clock, cache);
    }

    [Fact]
    public async Task General_level_ranking_includes_clan_and_filters_class()
    {
        var (svc, _, _, _, cache) = Create();
        using var _c = cache;

        var all = await svc.GetLevelAsync(LevelPeriod.General, null, 1, 50, default);
        var mages = await svc.GetLevelAsync(LevelPeriod.General, 7, 1, 50, default);

        Assert.Equal(4, all.Total);
        Assert.Equal("Guarda", all.Items[0].Clan);
        Assert.Equal(["Bravo"], mages.Items.Select(i => i.Name));
        Assert.Equal(1, mages.Items[0].Position);
    }

    [Fact]
    public async Task Period_without_any_snapshot_is_unavailable_instead_of_inventing_history()
    {
        var (svc, _, _, _, cache) = Create();
        using var _c = cache;

        var weekly = await svc.GetLevelAsync(LevelPeriod.Weekly, null, 1, 50, default);

        Assert.False(weekly.Available);
        Assert.Empty(weekly.Items);
        Assert.Equal(new DateOnly(2026, 11, 9), weekly.PeriodStart);
    }

    [Fact]
    public async Task Weekly_gains_count_from_first_snapshot_and_report_partial_period()
    {
        var (svc, snaps, _, _, cache) = Create();
        using var _c = cache;
        // Fotos começaram na terça, depois do início da semana (segunda 09/11).
        snaps.Days[new DateOnly(2026, 11, 10)] = new(StringComparer.OrdinalIgnoreCase) { ["Alfa"] = 118, ["Bravo"] = 105, ["Charlie"] = 90 };

        var weekly = await svc.GetLevelAsync(LevelPeriod.Weekly, null, 1, 50, default);

        Assert.True(weekly.Available);
        Assert.Equal(new DateOnly(2026, 11, 10), weekly.CountingSince);
        Assert.Equal(["Bravo", "Alfa"], weekly.Items.Select(i => i.Name));
        Assert.Equal(5, weekly.Items[0].LevelsGained);
        Assert.DoesNotContain(weekly.Items, i => i.Name == "Novo");
        Assert.DoesNotContain(weekly.Items, i => i.Name == "Charlie");
    }

    [Fact]
    public async Task Daily_uses_todays_snapshot_only()
    {
        var (svc, snaps, _, _, cache) = Create();
        using var _c = cache;
        snaps.Days[new DateOnly(2026, 11, 10)] = new(StringComparer.OrdinalIgnoreCase) { ["Alfa"] = 100 };

        var daily = await svc.GetLevelAsync(LevelPeriod.Daily, null, 1, 50, default);

        Assert.False(daily.Available);
        Assert.Equal(new DateOnly(2026, 11, 11), daily.PeriodStart);
    }

    [Fact]
    public async Task Pvp_rankings_order_by_kind_and_skip_zeros()
    {
        var (svc, _, _, _, cache) = Create();
        using var _c = cache;

        var bp = await svc.GetPvpAsync(PvpKind.BattlePoints, null, 1, 50, default);
        var weekly = await svc.GetPvpAsync(PvpKind.Weekly, null, 1, 50, default);

        Assert.Equal(["Bravo", "Alfa"], bp.Items.Select(i => i.Name));
        Assert.Equal(["Alfa", "Bravo"], weekly.Items.Select(i => i.Name));
        Assert.True(weekly.Items[0].IsPvpKing);
    }

    [Fact]
    public async Task Clans_tie_on_points_break_by_members()
    {
        var (svc, _, _, _, cache) = Create();
        using var _c = cache;

        var clans = await svc.GetClansAsync(1, 50, default);

        Assert.Equal(["Lobos", "Guarda"], clans.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Character_profile_and_search()
    {
        var (svc, _, _, _, cache) = Create();
        using var _c = cache;

        var alfa = await svc.GetCharacterAsync("alfa", default);
        var found = await svc.SearchAsync("ra", default);

        Assert.Equal(1, alfa!.LevelPosition);
        Assert.Equal(4, alfa.LevelTotal);
        Assert.Equal("Guarda", alfa.Clan);
        Assert.Equal(2, alfa.Pvp!.Position);
        Assert.Null(await svc.GetCharacterAsync("ninguem", default));
        Assert.Equal(["Bravo"], found.Select(f => f.Name));
        Assert.Empty(await svc.SearchAsync("a", default));
    }

    [Fact]
    public async Task Paging_is_clamped()
    {
        var (svc, _, _, _, cache) = Create();
        using var _c = cache;

        var page = await svc.GetLevelAsync(LevelPeriod.General, null, 2, 3, default);
        var huge = await svc.GetLevelAsync(LevelPeriod.General, null, 0, 5000, default);

        Assert.Equal(["Novo"], page.Items.Select(i => i.Name));
        Assert.Equal(1, huge.Page);
        Assert.Equal(RankingQueryService.MaxPageSize, huge.PageSize);
    }

    [Fact]
    public async Task Snapshot_is_taken_once_per_local_day_and_old_ones_are_pruned()
    {
        var levels = new Levels(new CharacterLevelRow("Alfa", 1, 120));
        var snaps = new Snapshots();
        snaps.Days[new DateOnly(2025, 1, 1)] = [];
        var clock = new Clock(new DateTimeOffset(2026, 11, 11, 2, 30, 0, TimeSpan.Zero)); // 10/11 23:30 em Brasília
        var svc = new LevelSnapshotService(levels, snaps, new RankingPeriods(), clock, NullLogger<LevelSnapshotService>.Instance);

        Assert.True(await svc.TakeIfMissingAsync(default));
        Assert.False(await svc.TakeIfMissingAsync(default));
        Assert.True(snaps.Days.ContainsKey(new DateOnly(2026, 11, 10)));
        Assert.False(snaps.Days.ContainsKey(new DateOnly(2025, 1, 1)));
    }
}
