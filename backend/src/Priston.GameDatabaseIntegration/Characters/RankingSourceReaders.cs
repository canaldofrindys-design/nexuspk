using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.GameDatabaseIntegration.Connection;
using static Priston.GameDatabaseIntegration.SqlIdentifier;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>Ranking PvP de <c>UserDB.dbo.BattlePoints</c>, sem contas de GM. A conta nunca sai daqui.</summary>
internal sealed class PvpRankingReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IPvpRankingReader
{
    public async Task<IReadOnlyList<PvpRow>> ReadAllAsync(CancellationToken ct)
    {
        var o = options.Value;
        var sql = $"""
            SELECT bp.Nick AS Name, ISNULL(bp.Class, 0) AS ClassCode, ISNULL(bp.BPs, 0) AS BattlePoints,
                   bp.BPsSemanal AS WeeklyBattlePoints, bp.KillsPvP AS Kills, bp.MortesPvP AS Deaths,
                   CAST(CASE WHEN bp.IsReiPvP = 1 THEN 1 ELSE 0 END AS bit) AS IsPvpKing
            FROM {Quote(o.UserDatabase)}.dbo.BattlePoints AS bp
            WHERE bp.Nick IS NOT NULL AND LTRIM(RTRIM(bp.Nick)) <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM {Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                  WHERE gm.Account COLLATE DATABASE_DEFAULT = bp.Account COLLATE DATABASE_DEFAULT)
            """;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<PvpRow>(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => r with { Name = r.Name.Trim() }).ToList();
    }
}

/// <summary>Ranking de clãs de <c>ClanDB.dbo.CL</c>. Não lê ClanZang (líder), que pode ser uma conta.</summary>
internal sealed class ClanRankingReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IClanRankingReader
{
    public async Task<IReadOnlyList<ClanRow>> ReadAllAsync(CancellationToken ct)
    {
        var o = options.Value;
        var sql = $"""
            SELECT ClanName AS Name, MemCnt AS Members, ISNULL(Cpoint, 0) AS Points, ISNULL(CWin, 0) AS Wins,
                   ISNULL(CFail, 0) AS Losses, RegiDate AS CreatedLocal
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
