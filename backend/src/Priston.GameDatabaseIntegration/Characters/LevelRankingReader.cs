using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>
/// Lê o ranking por nível de <c>ServerDB.dbo.LevelTable</c> (szID, Nick, Class, Level),
/// excluindo contas de <c>UserDB.dbo.GameMasters</c>. Ver DATABASE_MAPPING.md, seção 3.
/// A tabela não tem EXP nem índice: o resultado deve ser cacheado por quem chama.
/// </summary>
internal sealed class LevelRankingReader(
    IGameDbConnectionFactory connections,
    IOptions<GameDatabaseOptions> options) : ILevelRankingReader
{
    public async Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var sql = $"""
            SELECT lt.Nick AS Name, lt.Class AS ClassCode, lt.Level AS Level
            FROM {SqlIdentifier.Quote(o.ServerDatabase)}.dbo.LevelTable AS lt
            WHERE lt.Level > 0
              AND lt.Nick IS NOT NULL AND LTRIM(RTRIM(lt.Nick)) <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM {SqlIdentifier.Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                  WHERE gm.Account COLLATE DATABASE_DEFAULT = lt.szID COLLATE DATABASE_DEFAULT)
            """;

        await using var connection = await connections.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CharacterLevelRow>(
            new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => r with { Name = r.Name.Trim() }).ToList();
    }
}
