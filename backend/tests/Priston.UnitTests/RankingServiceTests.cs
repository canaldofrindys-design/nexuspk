using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Characters;

namespace Priston.UnitTests;

public class RankingServiceTests
{
    private sealed class FakeReader(IReadOnlyList<CharacterLevelRow> rows) : ILevelRankingReader
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(rows);
        }
    }

    [Fact]
    public void Orders_by_level_then_name_and_assigns_positions()
    {
        var ranked = RankingService.Rank(
        [
            new("bravo", 1, 150),
            new("Alpha", 7, 200),
            new("charlie", 8, 200),
        ]);

        Assert.Equal(["Alpha", "charlie", "bravo"], ranked.Select(r => r.Name));
        Assert.Equal([1, 2, 3], ranked.Select(r => r.Position));
        Assert.Equal("Mago", ranked[0].ClassName);
    }

    [Fact]
    public async Task Reads_the_table_once_while_cached()
    {
        var reader = new FakeReader([new("a", 1, 10)]);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new RankingService(reader, cache);

        await service.GetTopAsync(5, CancellationToken.None);
        await service.GetTopAsync(5, CancellationToken.None);

        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Top_is_limited_to_requested_count()
    {
        var rows = Enumerable.Range(1, 20).Select(i => new CharacterLevelRow($"p{i:00}", 1, i)).ToList();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var top = await new RankingService(new FakeReader(rows), cache).GetTopAsync(5, CancellationToken.None);

        Assert.Equal(5, top.Count);
        Assert.Equal(20, top[0].Level);
    }

    [Fact]
    public void Ranking_uses_the_character_file_when_the_level_table_is_behind()
    {
        var files = new Dictionary<string, Priston.Application.Player.CharacterFileLevel>(StringComparer.OrdinalIgnoreCase)
        {
            ["Frindys"] = new("Frindys", 11, 200),
            ["Gm200"] = new("Gm200", 3, 200),
            ["Antigo"] = new("Antigo", 1, 130),
            ["SemClasse"] = new("SemClasse", 0, 50),
        };

        var rows = Priston.GameDatabaseIntegration.Characters.LevelRankingReader.WithFileLevels(
        [
            new("Frindys", 0, 0),
            new("Antigo", 1, 140),
            new("Mago", 7, 150),
            new("SemClasse", 0, 0),
        ], files);

        Assert.Equal(["Frindys", "Antigo", "Mago"], rows.Select(r => r.Name));
        Assert.Equal(new CharacterLevelRow("Frindys", 11, 200), rows[0]);
        Assert.Equal(140, rows[1].Level);
        Assert.DoesNotContain(rows, r => r.Name == "Gm200");
    }
}
