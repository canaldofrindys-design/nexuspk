using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Admin;
using Priston.GameDatabaseIntegration.Accounts;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Admin;

/// <summary>
/// Totais para o painel: contas somando as 36 tabelas &lt;letra&gt;GameUser e personagens distintos da LevelTable.
/// Só leitura, com o login de leitura. Ver DATABASE_MAPPING.md.
/// </summary>
internal sealed class GameStatsReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IGameStatsReader
{
    public async Task<GameTotals> ReadAsync(CancellationToken ct)
    {
        var o = options.Value;
        var db = SqlIdentifier.Quote(o.AccountDatabase);
        var union = string.Join("\n UNION ALL ", GameAccountTable.AllShards.Select(s => $"SELECT COUNT_BIG(*) AS c FROM {db}.[dbo].[{s}GameUser]"));
        var sql = $"""
            SELECT (SELECT SUM(c) FROM ({union}) AS t) AS Accounts,
                   (SELECT COUNT(DISTINCT Nick) FROM {SqlIdentifier.Quote(o.ServerDatabase)}.dbo.LevelTable WHERE Level > 0 AND Nick IS NOT NULL) AS Characters
            """;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var row = await conn.QuerySingleAsync<(long Accounts, int Characters)>(
            new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return new GameTotals(row.Accounts, row.Characters);
    }
}
