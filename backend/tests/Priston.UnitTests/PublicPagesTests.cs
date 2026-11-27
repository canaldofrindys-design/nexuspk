using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Characters;
using Priston.Application.Home;
using Priston.Infrastructure.GameServer;

namespace Priston.UnitTests;

public class PublicPagesTests
{
    private const string Level = "·¹º§";
    private const string Exp = "°æÇèÄ¡";
    private const string Item = "¾ÆÀÌÅÛ";

    private static string MonsterText(string name, string drops) =>
        $"*Name \"{name}\"\r\n*{Level}\t\t87\r\n*{Exp}\t\t125000\r\n*{Item}\t\t{drops}\r\n";

    private sealed class Catalog(params MonsterInfo[] monsters) : IMonsterDropCatalog
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>());

        public Task<IReadOnlyList<MonsterInfo>> ReadMonstersAsync(CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<MonsterInfo>>(monsters);
        }
    }

    private sealed class BrokenCatalog : IMonsterDropCatalog
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct) => throw new DirectoryNotFoundException();

        public Task<IReadOnlyList<MonsterInfo>> ReadMonstersAsync(CancellationToken ct) => throw new DirectoryNotFoundException();
    }

    [Fact]
    public void Monster_file_gives_level_and_exp_when_present()
    {
        var m = MonsterFileCatalog.ParseDetails("Mapa_1_Lobo.inf", MonsterText("Lobo", "4 WA101 WA102"));

        Assert.NotNull(m);
        Assert.Equal(87, m.Level);
        Assert.Equal(125000, m.Exp);
    }

    [Fact]
    public void Monster_file_without_level_keeps_it_empty()
    {
        var m = MonsterFileCatalog.ParseDetails("Lobo.inf", $"*Name \"Lobo\"\r\n*{Item}\t\t4 WA101\r\n");

        Assert.NotNull(m);
        Assert.Null(m.Level);
        Assert.Null(m.Exp);
    }

    [Fact]
    public async Task Monster_guide_lists_once_per_monster_and_finds_by_item()
    {
        var drops = new List<MonsterDropGroup> { new("4", ["WA101", "WA102"], null), new("50", [], "100–200") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var guide = new MonsterGuide(cache, new Catalog(
            new MonsterInfo("Lobo", "Mapa2_Lobo.inf", 900, drops, 20),
            new MonsterInfo("Lobo", "Mapa1_Lobo.inf", 900, drops, 20),
            new MonsterInfo("Valento", "Boss_Valento.inf", 90000, [new("2", ["OS110"], null)], 80)));

        var all = await guide.ListAsync(null, default);
        var byItem = await guide.ListAsync("os110", default);
        var page = await guide.GetAsync("mapa1_lobo", default);

        Assert.True(all.Available);
        Assert.Equal(["Lobo", "Valento"], all.Monsters.Select(m => m.Name));
        Assert.Equal("Mapa1_Lobo", all.Monsters[0].Slug);
        Assert.Equal(2, all.Monsters[0].Items);
        Assert.True(all.Monsters[0].Gold);
        Assert.Equal(["Valento"], byItem.Monsters.Select(m => m.Name));
        Assert.NotNull(page);
        Assert.Equal("2", page.Drops[0].Items[0].PerKill);
        Assert.Null(await guide.GetAsync("Mapa2_Lobo", default));
        Assert.Null(await guide.GetAsync("../../Windows/win.ini", default));
    }

    [Fact]
    public async Task Monster_guide_is_unavailable_when_folder_fails()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var guide = new MonsterGuide(cache, new BrokenCatalog());

        var list = await guide.ListAsync(null, default);

        Assert.False(list.Available);
        Assert.Empty(list.Monsters);
    }

    [Theory]
    [InlineData("4", 2, "2")]
    [InlineData("1", 3, "0.33")]
    [InlineData("4", 1, null)]
    [InlineData("x", 2, null)]
    public void Per_kill_chance_divides_line_by_items(string chance, int choices, string? expected) =>
        Assert.Equal(expected, MonsterGuide.PerKill(chance, choices));

    [Fact]
    public void Level_ups_come_from_consecutive_snapshots_and_today()
    {
        var d = new DateOnly(2026, 11, 20);
        var points = new List<LevelSnapshotPoint>
        {
            new(d, "Alfa", 100), new(d.AddDays(1), "Alfa", 102), new(d.AddDays(4), "Alfa", 105),
            new(d, "Bravo", 50), new(d.AddDays(1), "Bravo", 50),
            new(d, "Gm", 10), new(d.AddDays(1), "Gm", 200),
        };
        var current = new List<RankedCharacter> { new(1, "Alfa", 1, "Lutador", 107), new(2, "Bravo", 7, "Mago", 50) };

        var events = PublicHistoryService.BuildLevelUps(points, current, d.AddDays(5));

        Assert.Equal(3, events.Count);
        Assert.True(events[0].Today);
        Assert.Equal((105, 107), (events[0].FromLevel, events[0].ToLevel));
        Assert.Equal(d.AddDays(4), events[0].SinceDay);
        Assert.Equal((102, 105, d.AddDays(3), d.AddDays(1)), (events[1].FromLevel, events[1].ToLevel, events[1].Day, events[1].SinceDay));
        Assert.Equal((100, 102, d, (DateOnly?)null), (events[2].FromLevel, events[2].ToLevel, events[2].Day, events[2].SinceDay));
        Assert.DoesNotContain(events, e => e.Name == "Gm" || e.Name == "Bravo");
    }

    [Fact]
    public void Clan_roster_has_only_public_characters_with_leader_first()
    {
        var clan = new ClanRow("Lobos", 3, 300, 1, 0, null, 7, "Beta");
        var ranking = new List<RankedCharacter> { new(1, "Alfa", 1, "Lutador", 150), new(2, "Beta", 7, "Mago", 120), new(3, "Fora", 2, "Mecânico", 110) };
        var clanOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["alfa"] = "LOBOS", ["Beta"] = "Lobos", ["Gm"] = "Lobos", ["Fora"] = "Outro" };
        var bp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Alfa"] = 40 };

        var roster = RankingQueryService.BuildRoster(clan, ranking, clanOf, bp);

        Assert.Equal(["Beta", "Alfa"], roster.Select(m => m.Name));
        Assert.True(roster[0].IsLeader);
        Assert.Equal(40, roster[1].BattlePoints);
    }
}
