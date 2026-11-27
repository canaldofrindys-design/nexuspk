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

/// <summary><paramref name="PvpPoints"/>: <c>PontosPvP</c>, que decide a patente; <paramref name="Patent"/>: nível dela (0 = nenhuma, 8 = Rei PvP).</summary>
public sealed record PvpEntry(int Position, string Name, int ClassCode, string ClassName, int BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths, bool IsPvpKing, int PvpPoints = 0)
{
    public int Patent => PvpPatents.LevelOf(PvpPoints, IsPvpKing);
}

/// <summary>
/// Uma patente PvP do jogo com quem está nela agora. <paramref name="Players"/>: quantos personagens têm essa patente;
/// <paramref name="Top"/>: o de mais pontos PvP nela (no Rei, o próprio rei).
/// </summary>
public sealed record PvpTier(int Level, string Key, string Name, int? MinPoints, int? MaxPoints, string Bonus, string Emblem, int Players, PvpEntry? Top);

/// <summary>As patentes da primeira à Rei PvP. <paramref name="Total"/>: personagens com alguma patente.</summary>
public sealed record PvpTierLadder(int Total, IReadOnlyList<PvpTier> Tiers);

public sealed record ClanEntry(int Position, string Name, int Members, int Points, int Wins, int Losses, DateTimeOffset? CreatedUtc);

/// <summary>Membro do clã na página pública: personagem do ranking de nível (sem GMs nem contas escondidas), com BP quando tem.</summary>
public sealed record ClanMember(string Name, int ClassCode, string ClassName, int Level, int LevelPosition, int BattlePoints, bool IsLeader);

/// <summary>
/// Página pública de um clã (<c>ClanDB.dbo.CL</c> e <c>UL</c>). <paramref name="Position"/>: lugar no ranking de clãs (pontos do SoD).
/// <paramref name="Leader"/>: personagem líder (<c>ClanZang</c>), só quando ele aparece no ranking público.
/// <paramref name="Roster"/>: membros que aparecem no ranking de nível, do maior nível para o menor.
/// </summary>
public sealed record ClanProfile(
    string Name,
    int IconId,
    string? Leader,
    int Members,
    int Points,
    int Position,
    int Total,
    int Wins,
    int Losses,
    DateTimeOffset? CreatedUtc,
    int TotalBattlePoints,
    IReadOnlyList<ClanMember> Roster);

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

    /// <summary>
    /// Quem está em cada patente do jogo agora, pelos pontos PvP (<c>PontosPvP</c>) e pelo Rei PvP (<c>IsReiPvP</c>).
    /// A regra é a do cliente (<see cref="PvpPatents"/>).
    /// </summary>
    public async Task<PvpTierLadder> GetPvpTiersAsync(CancellationToken ct)
    {
        var entries = (await AllPvpAsync(ct).ConfigureAwait(false))
            .OrderByDescending(r => r.PvpPoints).ThenByDescending(r => r.BattlePoints).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => ToPvpEntry(r, i + 1))
            .ToList();
        return BuildTiers(entries);
    }

    /// <summary>Agrupa os personagens pela patente (da primeira à Rei PvP); quem não tem patente fica de fora.</summary>
    public static PvpTierLadder BuildTiers(IReadOnlyList<PvpEntry> players)
    {
        var byLevel = players
            .Where(p => p.Patent > 0)
            .GroupBy(p => p.Patent)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PvpPoints).ThenByDescending(p => p.BattlePoints).ToList());
        var tiers = PvpPatents.All
            .Select(t =>
            {
                var members = byLevel.GetValueOrDefault(t.Level) ?? [];
                return new PvpTier(t.Level, t.Key, t.Name, t.MinPoints, t.MaxPoints, t.Bonus, t.Emblem, members.Count, members.FirstOrDefault());
            })
            .ToList();
        return new PvpTierLadder(tiers.Sum(t => t.Players), tiers);
    }

    /// <summary>Todos os clãs de <c>ClanDB.dbo.CL</c>, do cache (5 minutos).</summary>
    public async Task<IReadOnlyList<ClanRow>> GetAllClansAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync("ranking:clans", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await clanReader.ReadAllAsync(ct);
        }) ?? [];

    public async Task<RankingPage<ClanEntry>> GetClansAsync(int page, int pageSize, CancellationToken ct)
    {
        var rows = await GetAllClansAsync(ct);
        var entries = rows
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Members)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => new ClanEntry(i + 1, r.Name, r.Members, r.Points, r.Wins, r.Losses, ToUtc(r.CreatedLocal)))
            .ToList();
        return Paged(entries, page, pageSize, true, null, null, clock.GetUtcNow());
    }

    /// <summary>Página do clã pelo nome (sem diferença de maiúsculas). null quando o clã não existe.</summary>
    public async Task<ClanProfile?> GetClanAsync(string name, CancellationToken ct)
    {
        var rows = await GetAllClansAsync(ct);
        var ordered = rows
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Members)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var index = ordered.FindIndex(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        var clan = ordered[index];
        var levelsAll = await levels.GetLevelRankingAsync(ct);
        var clans = await ClansByCharacterAsync(ct);
        var bp = (await AllPvpAsync(ct)).ToDictionary(r => r.Name, r => r.BattlePoints, StringComparer.OrdinalIgnoreCase);
        var roster = BuildRoster(clan, levelsAll, clans, bp);
        var leader = roster.FirstOrDefault(m => m.IsLeader)?.Name;
        return new ClanProfile(
            clan.Name, clan.IconId, leader, clan.Members, clan.Points, index + 1, ordered.Count, clan.Wins, clan.Losses,
            ToUtc(clan.CreatedLocal), roster.Sum(m => m.BattlePoints), roster);
    }

    /// <summary>
    /// Membros do clã pelo <c>UL</c> que estão no ranking público de nível (GMs e escondidos ficam de fora), líder primeiro,
    /// depois do maior nível para o menor.
    /// </summary>
    public static IReadOnlyList<ClanMember> BuildRoster(ClanRow clan, IReadOnlyList<RankedCharacter> ranking, IReadOnlyDictionary<string, string> clanOf, IReadOnlyDictionary<string, int> battlePoints) =>
        ranking
            .Where(c => clanOf.TryGetValue(c.Name, out var cn) && string.Equals(cn, clan.Name, StringComparison.OrdinalIgnoreCase))
            .Select(c => new ClanMember(c.Name, c.ClassCode, c.ClassName, c.Level, c.Position, battlePoints.GetValueOrDefault(c.Name),
                clan.Leader is not null && string.Equals(clan.Leader, c.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(m => m.IsLeader)
            .ThenBy(m => m.LevelPosition)
            .ToList();

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

    /// <summary>Clã de cada personagem (ClanDB.UL), do cache. Sem o ClanDB, vazio.</summary>
    public Task<IReadOnlyDictionary<string, string>> GetClanByCharacterAsync(CancellationToken ct) => ClansByCharacterAsync(ct);

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
            r.BattlePoints, r.WeeklyBattlePoints, r.Kills, r.Deaths, r.IsPvpKing, r.PvpPoints);

    private static RankingPage<T> Paged<T>(List<T> all, int page, int pageSize, bool available, DateOnly? start, DateOnly? since, DateTimeOffset now)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var p = Math.Max(1, page);
        return new RankingPage<T>(all.Skip((p - 1) * size).Take(size).ToList(), all.Count, p, size, available, start, since, now);
    }

    private static DateTimeOffset? ToUtc(DateTime? local) =>
        local is null ? null : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified), GameTimeZone), TimeSpan.Zero);
}
