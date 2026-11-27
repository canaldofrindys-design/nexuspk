using Microsoft.Extensions.Caching.Memory;
using Priston.Domain.Characters;
using Priston.Domain.Time;

namespace Priston.Application.Characters;

/// <summary>Personagem no ranking do SoD (Bellatra). <paramref name="Level"/>: o nível gravado pelo SoD ou o da LevelTable.</summary>
public sealed record SodEntry(int Position, string Name, int ClassCode, string ClassName, int Level, int Points, string? Clan);

/// <summary>
/// Clã no ranking do SoD, pelos pontos que o jogo soma em <c>ClanDB.CL.Cpoint</c>.
/// <paramref name="Crown"/>: 1 ouro, 2 prata, 3 bronze (as coroas que o SoD dá aos três primeiros); 0 sem coroa.
/// </summary>
public sealed record SodClanEntry(int Position, string Name, int Points, int Members, int Wins, int Losses, int IconId, string? Leader, int Crown);

/// <summary>
/// Meses que o ranking do SoD pode mostrar. <paramref name="Current"/>: o mês corrente em Brasília (pode ainda não ter tabela);
/// <paramref name="Months"/>: os que têm tabela no sodDB, do mais novo para o mais antigo (no máximo <see cref="SodRankingService.MaxMonths"/>).
/// <paramref name="Available"/> false: o sodDB não respondeu.
/// </summary>
public sealed record SodMonths(DateOnly Current, IReadOnlyList<DateOnly> Months, bool Available);

/// <summary>
/// Ranking do Survival of the Death (Bellatra): solo por mês (<c>sodDB.dbo.SOD2Record&lt;aamm&gt;</c>), solo da semana
/// (<c>UserDB.BattlePoints.SodSemanal</c>) e por clã (<c>ClanDB.CL.Cpoint</c>). Tudo do cache, como os outros rankings.
/// </summary>
public sealed class SodRankingService(
    ISodRankingReader reader,
    RankingQueryService rankings,
    RankingService levels,
    IMemoryCache cache,
    TimeProvider clock)
{
    /// <summary>Quantos meses o seletor oferece.</summary>
    public const int MaxMonths = 12;

    private static readonly TimeZoneInfo GameTimeZone = TimeZoneInfo.FindSystemTimeZoneById(RankingPeriods.TimeZoneId);

    public DateOnly CurrentMonth()
    {
        var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), GameTimeZone);
        return new DateOnly(local.Year, local.Month, 1);
    }

    public async Task<SodMonths> GetMonthsAsync(CancellationToken ct)
    {
        var current = CurrentMonth();
        try
        {
            var months = await cache.GetOrCreateAsync("ranking:sod:months", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = RankingService.CacheDuration;
                return await reader.ReadMonthsAsync(ct).ConfigureAwait(false);
            }).ConfigureAwait(false) ?? [];
            return new SodMonths(current, months.Where(m => m <= current).Take(MaxMonths).ToList(), true);
        }
#pragma warning disable CA1031 // Sem o sodDB a página continua com a semana e os clãs.
        catch (Exception) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            return new SodMonths(current, [], false);
        }
    }

    /// <summary>
    /// Ranking solo do mês (<paramref name="month"/> null: o corrente). Tabela do mês inexistente ou sodDB fora: Available false.
    /// PeriodStart é o primeiro dia do mês mostrado.
    /// </summary>
    public async Task<RankingPage<SodEntry>> GetMonthlyAsync(DateOnly? month, int? classCode, int page, int pageSize, CancellationToken ct)
    {
        var m = month ?? CurrentMonth();
        IReadOnlyList<SodPlayerRow>? rows;
        try
        {
            rows = await cache.GetOrCreateAsync($"ranking:sod:{m:yyyyMM}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = RankingService.CacheDuration;
                return await reader.ReadMonthAsync(m, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // sodDB sem permissão ou fora do ar: a aba aparece como indisponível.
        catch (Exception) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            rows = null;
        }

        if (rows is null)
        {
            return Paged(new List<SodEntry>(), page, pageSize, false, m);
        }

        var clans = await rankings.GetClanByCharacterAsync(ct).ConfigureAwait(false);
        var entries = rows
            .Where(r => classCode is null || r.ClassCode == classCode)
            .Select((r, i) => new SodEntry(i + 1, r.Name, r.ClassCode, ClassName(r.ClassCode), r.Level, r.Points, clans.GetValueOrDefault(r.Name)))
            .ToList();
        return Paged(entries, page, pageSize, true, m);
    }

    /// <summary>Ranking solo da semana, pelo contador do próprio jogo (<c>SodSemanal</c>). Nível pela LevelTable.</summary>
    public async Task<RankingPage<SodEntry>> GetWeeklyAsync(int? classCode, int page, int pageSize, CancellationToken ct)
    {
        var rows = await rankings.GetAllPvpAsync(ct).ConfigureAwait(false);
        var clans = await rankings.GetClanByCharacterAsync(ct).ConfigureAwait(false);
        IReadOnlyList<RankedCharacter> characters = [];
        try
        {
            characters = await levels.GetLevelRankingAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Sem a LevelTable o ranking continua, só sem o nível.
        catch (Exception) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
        }

        var level = characters
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(c => c.Level), StringComparer.OrdinalIgnoreCase);
        var entries = rows
            .Where(r => r.WeeklySod > 0 && (classCode is null || r.ClassCode == classCode))
            .OrderByDescending(r => r.WeeklySod)
            .ThenByDescending(r => level.GetValueOrDefault(r.Name))
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => new SodEntry(i + 1, r.Name, r.ClassCode, ClassName(r.ClassCode), level.GetValueOrDefault(r.Name), r.WeeklySod, clans.GetValueOrDefault(r.Name)))
            .ToList();
        return Paged(entries, page, pageSize, true, null);
    }

    /// <summary>Clãs com pontos no SoD (<c>CL.Cpoint</c> maior que zero), na mesma ordem do "Líder do SoD" da home.</summary>
    public async Task<RankingPage<SodClanEntry>> GetClansAsync(int page, int pageSize, CancellationToken ct)
    {
        var rows = await rankings.GetAllClansAsync(ct).ConfigureAwait(false);
        return Paged(RankClans(rows), page, pageSize, true, null);
    }

    public static List<SodClanEntry> RankClans(IEnumerable<ClanRow> rows) =>
        rows
            .Where(c => c.Points > 0)
            .OrderByDescending(c => c.Points)
            .ThenByDescending(c => c.Members)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select((c, i) => new SodClanEntry(i + 1, c.Name, c.Points, c.Members, c.Wins, c.Losses, c.IconId, c.Leader, i < 3 ? i + 1 : 0))
            .ToList();

    private static string ClassName(int code) => CharacterClassNames.ToPortuguese(CharacterClassNames.FromCode(code));

    private RankingPage<T> Paged<T>(List<T> all, int page, int pageSize, bool available, DateOnly? start)
    {
        var size = Math.Clamp(pageSize, 1, RankingQueryService.MaxPageSize);
        var p = Math.Max(1, page);
        return new RankingPage<T>(all.Skip((p - 1) * size).Take(size).ToList(), all.Count, p, size, available, start, null, clock.GetUtcNow());
    }
}
