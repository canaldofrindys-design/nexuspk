using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.GameDatabaseIntegration.Connection;
using static Priston.GameDatabaseIntegration.SqlIdentifier;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>
/// Ranking PvP de <c>UserDB.dbo.BattlePoints</c>, sem contas de GM, sem a lista de <see cref="IRankingExclusionSource"/> e sem
/// classes desconhecidas. A conta nunca sai daqui.
/// </summary>
internal sealed class PvpRankingReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options, IRankingExclusionSource exclusions) : IPvpRankingReader
{
    public async Task<IReadOnlyList<PvpRow>> ReadAllAsync(CancellationToken ct)
    {
        var o = options.Value;
        var sql = $"""
            SELECT bp.Account AS Account, bp.Nick AS Name, ISNULL(bp.Class, 0) AS ClassCode, ISNULL(bp.BPs, 0) AS BattlePoints,
                   bp.BPsSemanal AS WeeklyBattlePoints, bp.KillsPvP AS Kills, bp.MortesPvP AS Deaths,
                   CAST(CASE WHEN bp.IsReiPvP = 1 THEN 1 ELSE 0 END AS bit) AS IsPvpKing,
                   ISNULL(bp.PontosPvP, 0) AS PvpPoints, ISNULL(bp.SodSemanal, 0) AS WeeklySod
            FROM {Quote(o.UserDatabase)}.dbo.BattlePoints AS bp
            WHERE bp.Nick IS NOT NULL AND LTRIM(RTRIM(bp.Nick)) <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM {Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                  WHERE gm.Account COLLATE DATABASE_DEFAULT = bp.Account COLLATE DATABASE_DEFAULT)
            """;
        var hidden = await exclusions.GetAsync(ct).ConfigureAwait(false);
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return rows
            .Where(r => !hidden.Hides(r.Account, r.Name, r.ClassCode))
            .Select(r => new PvpRow(r.Name.Trim(), r.ClassCode, r.BattlePoints, r.WeeklyBattlePoints, r.Kills, r.Deaths, r.IsPvpKing, r.PvpPoints, r.WeeklySod))
            .ToList();
    }

    private sealed record Row(string? Account, string Name, int ClassCode, int BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths, bool IsPvpKing, int PvpPoints, int WeeklySod);
}

/// <summary>Ranking de clãs de <c>ClanDB.dbo.CL</c>. Lê ClanZang (personagem líder); a conta do líder (UserID) não.</summary>
internal sealed class ClanRankingReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IClanRankingReader
{
    public async Task<IReadOnlyList<ClanRow>> ReadAllAsync(CancellationToken ct)
    {
        var o = options.Value;
        var sql = $"""
            SELECT ClanName AS Name, MemCnt AS Members, ISNULL(Cpoint, 0) AS Points, ISNULL(CWin, 0) AS Wins,
                   ISNULL(CFail, 0) AS Losses, RegiDate AS CreatedLocal,
                   ISNULL(MIconCnt, 0) AS IconId, NULLIF(LTRIM(RTRIM(ClanZang)), '') AS Leader
            FROM {Quote(o.ClanDatabase)}.dbo.CL
            WHERE ClanName IS NOT NULL AND LTRIM(RTRIM(ClanName)) <> ''
            """;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<ClanRow>(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => r with { Name = r.Name.Trim() }).ToList();
    }
}

/// <summary>Clã de cada personagem, de <c>ClanDB.dbo.UL</c> (ChName, ClanName).</summary>
internal sealed class CharacterClanReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : ICharacterClanReader
{
    public async Task<IReadOnlyDictionary<string, string>> ReadAllAsync(CancellationToken ct)
    {
        var o = options.Value;
        var sql = $"""
            SELECT ChName AS Name, ClanName
            FROM {Quote(o.ClanDatabase)}.dbo.UL
            WHERE ChName IS NOT NULL AND ClanName IS NOT NULL AND LTRIM(RTRIM(ClanName)) <> ''
            """;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<(string Name, string ClanName)>(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, clan) in rows)
        {
            map.TryAdd(name.Trim(), clan.Trim());
        }

        return map;
    }
}

/// <summary>
/// SoD do mês em <c>sodDB.dbo.SOD2Record&lt;aamm&gt;</c> (uma tabela por mês, criada pelo servidor do jogo), como o
/// ranking Bellatra do site antigo. O nome da tabela sai só da data; a existência é conferida antes da consulta.
/// </summary>
internal sealed class SodRankingReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options, IRankingExclusionSource exclusions) : ISodRankingReader
{
    private const string TablePrefix = "SOD2Record";

    public async Task<IReadOnlyList<SodPlayerRow>?> ReadMonthAsync(DateOnly month, CancellationToken ct)
    {
        var o = options.Value;
        var table = TablePrefix + month.ToString("yyMM", System.Globalization.CultureInfo.InvariantCulture);
        var db = Quote(o.SodDatabase);
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var exists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM {db}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @table",
            new { table }, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        if (exists == 0)
        {
            return null;
        }

        // A tabela não tem chave: o mesmo personagem pode ter mais de uma linha no mês. Vale a maior pontuação.
        var sql = $"""
            SELECT t.Account, t.Name, MAX(t.ClassCode) AS ClassCode, MAX(t.Level) AS Level, MAX(t.Points) AS Points
            FROM (
                SELECT CAST(LTRIM(RTRIM(s.UserID)) AS nvarchar(50)) AS Account, CAST(LTRIM(RTRIM(s.CharName)) AS nvarchar(50)) AS Name,
                       CAST(ISNULL(s.CharType, 0) AS int) AS ClassCode, CAST(ISNULL(s.GLevel, 0) AS int) AS Level,
                       CAST(ISNULL(s.Point, 0) AS int) AS Points
                FROM {db}.dbo.{Quote(table)} AS s
                WHERE s.Point > 0 AND s.CharName IS NOT NULL AND LTRIM(RTRIM(s.CharName)) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM {Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                      WHERE gm.Account COLLATE DATABASE_DEFAULT = s.UserID COLLATE DATABASE_DEFAULT)
            ) AS t
            GROUP BY t.Account, t.Name
            """;
        var hidden = await exclusions.GetAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SodRow>(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return rows
            .Where(r => !hidden.Hides(r.Account, r.Name, r.ClassCode))
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.Points).ThenByDescending(r => r.Level).First())
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Level)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new SodPlayerRow(r.Name, r.ClassCode, r.Level, r.Points))
            .ToList();
    }

    public async Task<IReadOnlyList<DateOnly>> ReadMonthsAsync(CancellationToken ct)
    {
        var o = options.Value;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var names = await conn.QueryAsync<string>(new CommandDefinition(
            $"SELECT TABLE_NAME FROM {Quote(o.SodDatabase)}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME LIKE '{TablePrefix}[0-9][0-9][0-9][0-9]'",
            commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return names
            .Select(ParseMonth)
            .OfType<DateOnly>()
            .Distinct()
            .OrderDescending()
            .ToList();
    }

    /// <summary>"SOD2Record2611" → 01/11/2026. Nome fora do padrão: null.</summary>
    internal static DateOnly? ParseMonth(string table) =>
        table.Length == TablePrefix.Length + 4
        && table.StartsWith(TablePrefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(table.AsSpan(TablePrefix.Length, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var yy)
        && int.TryParse(table.AsSpan(TablePrefix.Length + 2, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var mm)
        && mm is >= 1 and <= 12
            ? new DateOnly(2000 + yy, mm, 1)
            : null;

    private sealed record SodRow(string? Account, string Name, int ClassCode, int Level, int Points);
}
