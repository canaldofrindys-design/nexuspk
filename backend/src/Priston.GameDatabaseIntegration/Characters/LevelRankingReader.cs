using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Player;
using Priston.Domain.Characters;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>
/// Lê o ranking por nível de <c>ServerDB.dbo.LevelTable</c> (szID, Nick, Class, Level),
/// excluindo contas de <c>UserDB.dbo.GameMasters</c>, a lista de <see cref="IRankingExclusionSource"/> e classes
/// desconhecidas. A conta só serve para o filtro e não sai daqui. Ver DATABASE_MAPPING.md, seção 3.
/// A tabela não tem EXP nem índice: o resultado deve ser cacheado por quem chama.
/// A LevelTable fica atrás do arquivo do personagem (.dat) em alguns casos: personagem novo com classe 0 e nível 0,
/// ou nível dado pela equipe (ex.: 200) que o servidor não gravou na tabela. Por isso valem o maior nível e,
/// quando a tabela não tem classe, a classe do arquivo. Pasta dos arquivos fora do alcance: só a tabela.
/// </summary>
internal sealed class LevelRankingReader(
    IGameDbConnectionFactory connections,
    IOptions<GameDatabaseOptions> options,
    IRankingExclusionSource exclusions,
    ICharacterFileReader? characterFiles = null) : ILevelRankingReader
{
    public async Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var sql = $"""
            SELECT lt.szID AS Account, lt.Nick AS Name, ISNULL(lt.Class, 0) AS ClassCode, lt.Level AS Level
            FROM {SqlIdentifier.Quote(o.ServerDatabase)}.dbo.LevelTable AS lt
            WHERE lt.Nick IS NOT NULL AND LTRIM(RTRIM(lt.Nick)) <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM {SqlIdentifier.Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                  WHERE gm.Account COLLATE DATABASE_DEFAULT = lt.szID COLLATE DATABASE_DEFAULT)
            """;

        var hidden = await exclusions.GetAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await connections.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var visible = rows
            .Where(r => !hidden.HidesPerson(r.Account, r.Name))
            .Select(r => new CharacterLevelRow(r.Name.Trim(), r.ClassCode, r.Level))
            .ToList();
        return WithFileLevels(visible, await FileLevelsAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Junta a tabela com os arquivos dos personagens: maior nível; classe da tabela, ou a do arquivo quando a da tabela
    /// é desconhecida. Fica de fora quem continua sem nível ou com classe desconhecida. Personagem que só existe em
    /// arquivo não entra: sem a linha da tabela não há a conta para conferir GMs e a lista de exclusão.
    /// </summary>
    public static IReadOnlyList<CharacterLevelRow> WithFileLevels(
        IEnumerable<CharacterLevelRow> rows, IReadOnlyDictionary<string, CharacterFileLevel> files) =>
        rows
            .Select(r =>
            {
                if (!files.TryGetValue(r.Name, out var f))
                {
                    return r;
                }

                var classCode = CharacterClassNames.IsKnown(r.ClassCode) || !CharacterClassNames.IsKnown(f.ClassCode) ? r.ClassCode : f.ClassCode;
                return r with { ClassCode = classCode, Level = Math.Max(r.Level, f.Level) };
            })
            .Where(r => r.Level > 0 && CharacterClassNames.IsKnown(r.ClassCode))
            .ToList();

    private async Task<IReadOnlyDictionary<string, CharacterFileLevel>> FileLevelsAsync(CancellationToken ct)
    {
        if (characterFiles is null)
        {
            return new Dictionary<string, CharacterFileLevel>();
        }

        try
        {
            return await characterFiles.ReadAllLevelsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<string, CharacterFileLevel>();
        }
    }

    private sealed record Row(string? Account, string Name, int ClassCode, int Level);
}
