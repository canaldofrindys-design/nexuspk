using Microsoft.Extensions.Caching.Memory;
using Priston.Domain.Characters;

namespace Priston.Application.Characters;

public sealed record RankedCharacter(int Position, string Name, int ClassCode, string ClassName, int Level);

/// <summary>
/// Ranking geral por nível (v1). Ordem: nível desc, nome asc.
/// A LevelTable é lida inteira no máximo uma vez a cada <see cref="CacheDuration"/>.
/// </summary>
public sealed class RankingService(ILevelRankingReader reader, IMemoryCache cache)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKey = "ranking:level:all";

    public async Task<IReadOnlyList<RankedCharacter>> GetLevelRankingAsync(CancellationToken cancellationToken)
    {
        var ranked = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var rows = await reader.ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return Rank(rows);
        }).ConfigureAwait(false);
        return ranked ?? [];
    }

    public async Task<IReadOnlyList<RankedCharacter>> GetTopAsync(int count, CancellationToken cancellationToken)
    {
        var all = await GetLevelRankingAsync(cancellationToken).ConfigureAwait(false);
        return all.Take(Math.Clamp(count, 1, 100)).ToList();
    }

    public static IReadOnlyList<RankedCharacter> Rank(IEnumerable<CharacterLevelRow> rows) =>
        rows.OrderByDescending(r => r.Level)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) =>
            {
                var cls = CharacterClassNames.FromCode(r.ClassCode);
                return new RankedCharacter(i + 1, r.Name, r.ClassCode, CharacterClassNames.ToPortuguese(cls), r.Level);
            })
            .ToList();
}
