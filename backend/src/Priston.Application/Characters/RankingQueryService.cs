using Microsoft.Extensions.Caching.Memory;
using Priston.Domain.Characters;
using Priston.Domain.Time;

namespace Priston.Application.Characters;

public enum LevelPeriod
{
    General,
    Daily,
    Weekly,
    Monthly,
}

public enum PvpKind
{
    BattlePoints,
    Weekly,
    Kills,
}

public sealed record LevelEntry(int Position, string Name, int ClassCode, string ClassName, int Level, int? LevelsGained, string? Clan);

public sealed record PvpEntry(int Position, string Name, int ClassCode, string ClassName, int BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths, bool IsPvpKing);

public sealed record ClanEntry(int Position, string Name, int Members, int Points, int Wins, int Losses, DateTimeOffset? CreatedUtc);

/// <summary>
/// Página de ranking. <paramref name="CountingSince"/>: data local a partir da qual o período é contado
/// (pode ser depois do início do período quando as fotos diárias começaram depois).
/// <paramref name="Available"/> false: não há foto para o período ou a fonte falhou.
/// </summary>
public sealed record RankingPage<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    bool Available,
    DateOnly? PeriodStart,
    DateOnly? CountingSince,
    DateTimeOffset GeneratedUtc);

public sealed record CharacterProfile(
    string Name,
    int ClassCode,
    string ClassName,
    int Level,
    int LevelPosition,
    int LevelTotal,
    string? Clan,
    PvpEntry? Pvp);

public sealed class RankingQueryService(
    RankingService levels,
    IPvpRankingReader pvpReader,
    IClanRankingReader clanReader,
    ICharacterClanReader clanMembers,
    ILevelSnapshotStore snapshots,
    RankingPeriods periods,
    IMemoryCache cache,
    TimeProvider clock)
{
    public const int MaxPageSize = 100;
    private static readonly TimeSpan CacheDuration = RankingService.CacheDuration;
    private static readonly TimeZoneInfo GameTimeZone = TimeZoneInfo.FindSystemTimeZoneById(RankingPeriods.TimeZoneId);

    public async Task<RankingPage<LevelEntry>> GetLevelAsync(LevelPeriod period, int? classCode, int page, int pageSize, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var current = await levels.GetLevelRankingAsync(ct);
        var clans = await ClansByCharacterAsync(ct);

        if (period == LevelPeriod.General)
        {
            var filtered = current.Where(c => classCode is null || c.ClassCode == classCode).ToList();
            var entries = filtered.Select((c, i) => new LevelEntry(i + 1, c.Name, c.ClassCode, c.ClassName, c.Level, null, clans.GetValueOrDefault(c.Name)));
            return Paged(entries.ToList(), page, pageSize, true, null, null, now);
        }

        var rp = period switch { LevelPeriod.Daily => RankingPeriod.Daily, LevelPeriod.Weekly => RankingPeriod.Weekly, _ => RankingPeriod.Monthly };
        var start = periods.StartDate(rp, now);
        var baseline = await cache.GetOrCreateAsync($"snapshot:from:{start:yyyyMMdd}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await snapshots.FirstOnOrAfterAsync(start, ct);
        });

        if (baseline is null)
        {
            return Paged(new List<LevelEntry>(), page, pageSize, false, start, null, now);
        }

        var gains = CalculateGains(current, baseline.Value.Levels, classCode);
        var ranked = gains.Select((g, i) => g with { Position = i + 1, Clan = clans.GetValueOrDefault(g.Name) }).ToList();
        return Paged(ranked, page, pageSize, true, start, baseline.Value.Date, now);
    }

    /// <summary>
    /// Níveis ganhos desde a foto. Personagem que não estava na foto fica de fora:
    /// não há como saber com que nível ele começou o período.
    /// </summary>
    public static IReadOnlyList<LevelEntry> CalculateGains(IEnumerable<RankedCharacter> current, IReadOnlyDictionary<string, int> baseline, int? classCode) =>
        current
            .Where(c => classCode is null || c.ClassCode == classCode)
            .Select(c => baseline.TryGetValue(c.Name, out var before) ? (c, gained: c.Level - before) : (c, gained: 0))
            .Where(x => x.gained > 0)
            .OrderByDescending(x => x.gained)
            .ThenByDescending(x => x.c.Level)
            .ThenBy(x => x.c.Name, StringComparer.OrdinalIgnoreCase)
            .Select((x, i) => new LevelEntry(i + 1, x.c.Name, x.c.ClassCode, x.c.ClassName, x.c.Level, x.gained, null))
            .ToList();

    public async Task<RankingPage<PvpEntry>> GetPvpAsync(PvpKind kind, int? classCode, int page, int pageSize, CancellationToken ct)
    {
        var rows = await AllPvpAsync(ct);
        var ordered = kind switch
        {
            PvpKind.Weekly => rows.Where(r => r.WeeklyBattlePoints > 0).OrderByDescending(r => r.WeeklyBattlePoints).ThenByDescending(r => r.BattlePoints),
            PvpKind.Kills => rows.Where(r => r.Kills > 0).OrderByDescending(r => r.Kills).ThenBy(r => r.Deaths),
            _ => rows.Where(r => r.BattlePoints > 0).OrderByDescending(r => r.BattlePoints).ThenByDescending(r => r.Kills),
        };
        var entries = ordered
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Where(r => classCode is null || r.ClassCode == classCode)
            .Select((r, i) => ToPvpEntry(r, i + 1))
            .ToList();
        return Paged(entries, page, pageSize, true, null, null, clock.GetUtcNow());
    }

    public async Task<RankingPage<ClanEntry>> GetClansAsync(int page, int pageSize, CancellationToken ct)
    {
        var rows = await cache.GetOrCreateAsync("ranking:clans", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await clanReader.ReadAllAsync(ct);
        }) ?? [];
        var entries = rows
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Members)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => new ClanEntry(i + 1, r.Name, r.Members, r.Points, r.Wins, r.Losses, ToUtc(r.CreatedLocal)))
            .ToList();
        return Paged(entries, page, pageSize, true, null, null, clock.GetUtcNow());
    }

    public async Task<CharacterProfile?> GetCharacterAsync(string name, CancellationToken ct)
    {
        var all = await levels.GetLevelRankingAsync(ct);
        var c = all.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (c is null)
        {
            return null;
        }

        var clans = await ClansByCharacterAsync(ct);
        var pvp = (await AllPvpAsync(ct))
            .Where(r => r.BattlePoints > 0)
            .OrderByDescending(r => r.BattlePoints).ThenByDescending(r => r.Kills).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => ToPvpEntry(r, i + 1))
            .FirstOrDefault(r => string.Equals(r.Name, c.Name, StringComparison.OrdinalIgnoreCase));
        return new CharacterProfile(c.Name, c.ClassCode, c.ClassName, c.Level, c.Position, all.Count, clans.GetValueOrDefault(c.Name), pvp);
    }

    public async Task<IReadOnlyList<RankedCharacter>> SearchAsync(string query, CancellationToken ct)
    {
        var q = query.Trim();
        if (q.Length < 2)
        {
            return [];
        }

        var all = await levels.GetLevelRankingAsync(ct);
        return all
            .Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(c => c.Position)
            .Take(20)
            .ToList();
    }

    private async Task<IReadOnlyList<PvpRow>> AllPvpAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync("ranking:pvp", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheDuration;
            // BattlePoints não tem chave: o mesmo personagem pode aparecer mais de uma vez. Fica a linha de maior BP.
            var rows = await pvpReader.ReadAllAsync(ct);
            return (IReadOnlyList<PvpRow>)rows
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(r => r.BattlePoints).ThenByDescending(r => r.Kills).First())
                .ToList();
        }) ?? [];

    private async Task<IReadOnlyDictionary<string, string>> ClansByCharacterAsync(CancellationToken ct)
    {
        try
        {
            return await cache.GetOrCreateAsync("ranking:clan-members", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await clanMembers.ReadAllAsync(ct);
            }) ?? new Dictionary<string, string>();
        }
#pragma warning disable CA1031 // Sem o ClanDB o ranking continua, só sem a coluna de clã.
        catch (Exception) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            return new Dictionary<string, string>();
        }
    }

    private static PvpEntry ToPvpEntry(PvpRow r, int position) =>
        new(position, r.Name, r.ClassCode, CharacterClassNames.ToPortuguese(CharacterClassNames.FromCode(r.ClassCode)),
            r.BattlePoints, r.WeeklyBattlePoints, r.Kills, r.Deaths, r.IsPvpKing);

    private static RankingPage<T> Paged<T>(List<T> all, int page, int pageSize, bool available, DateOnly? start, DateOnly? since, DateTimeOffset now)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var p = Math.Max(1, page);
        return new RankingPage<T>(all.Skip((p - 1) * size).Take(size).ToList(), all.Count, p, size, available, start, since, now);
    }

    private static DateTimeOffset? ToUtc(DateTime? local) =>
        local is null ? null : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified), GameTimeZone), TimeSpan.Zero);
}
