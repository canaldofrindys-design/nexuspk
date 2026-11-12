using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Server;
using Priston.GameDatabaseIntegration.Accounts;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Admin;

/// <summary>
/// Jogadores online: contas com <c>inuse = '1'</c> somando as 36 tabelas &lt;letra&gt;GameUser.
/// O servidor do jogo marca a coluna ao entrar (conferido na VPS em 11/11/2026 com uma conta no jogo).
/// Só leitura, com o login de leitura.
/// </summary>
internal sealed class OnlinePlayersReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IOnlinePlayersReader
{
    public async Task<int> CountAsync(CancellationToken ct)
    {
        var o = options.Value;
        var db = SqlIdentifier.Quote(o.AccountDatabase);
        var union = string.Join("\n UNION ALL ", GameAccountTable.AllShards.Select(s => $"SELECT COUNT_BIG(*) AS c FROM {db}.[dbo].[{s}GameUser] WHERE inuse = '1'"));
        var sql = $"SELECT CAST(SUM(c) AS int) FROM ({union}) AS t";
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
    }
}
