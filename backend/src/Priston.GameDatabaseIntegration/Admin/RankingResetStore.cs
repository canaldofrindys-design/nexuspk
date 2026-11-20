using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.GameAdmin;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Admin;

/// <summary>Zera e devolve rankings nas tabelas confirmadas (UserDB.BattlePoints, ClanDB.CL).</summary>
internal sealed class RankingResetStore(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IRankingResetStore
{
    private GameDatabaseOptions O => options.Value;

    public bool Enabled => factory.CanAdmin;

    private string TableOf(RankingResetKind kind) => kind.Table switch
    {
        "BattlePoints" => $"{SqlIdentifier.Quote(O.UserDatabase)}.dbo.[BattlePoints]",
        "CL" => $"{SqlIdentifier.Quote(O.ClanDatabase)}.dbo.[CL]",
        _ => throw new ArgumentException("Tabela de ranking desconhecida.", nameof(kind)),
    };

    public async Task<IReadOnlyList<RankingBackupRow>> ReadAsync(RankingResetKind kind, CancellationToken ct)
    {
        var cols = string.Join(", ", kind.KeyColumns.Concat(kind.ResetColumns).Select(SqlIdentifier.Quote));
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync(new CommandDefinition($"SELECT {cols} FROM {TableOf(kind)}", null, commandTimeout: O.CommandTimeoutSeconds * 4, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Cast<IDictionary<string, object?>>().Select(r => new RankingBackupRow(
            kind.KeyColumns.ToDictionary(k => k, k => r[k] is null ? null : Convert.ToString(r[k], CultureInfo.InvariantCulture)),
            kind.ResetColumns.ToDictionary(c => c, c => r[c] is null ? (long?)null : Convert.ToInt64(r[c], CultureInfo.InvariantCulture)))).ToList();
    }

    public async Task<int> ResetAsync(RankingResetKind kind, CancellationToken ct)
    {
        var set = string.Join(", ", kind.ResetColumns.Select(c => $"{SqlIdentifier.Quote(c)} = 0"));
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(new CommandDefinition($"SET XACT_ABORT ON; UPDATE {TableOf(kind)} SET {set};", null,
            commandTimeout: O.CommandTimeoutSeconds * 4, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<int> RestoreAsync(RankingResetKind kind, IReadOnlyList<RankingBackupRow> rows, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        var total = 0;
        foreach (var row in rows)
        {
            var p = new DynamicParameters();
            var set = new List<string>();
            var i = 0;
            foreach (var c in kind.ResetColumns)
            {
                // Coluna NOT NULL que estava nula não existe; nula volta como nula.
                p.Add($"v{i}", row.Values.GetValueOrDefault(c) is { } v ? (int)Math.Clamp(v, int.MinValue, int.MaxValue) : null, DbType.Int32);
                set.Add($"{SqlIdentifier.Quote(c)} = @v{i++}");
            }

            var where = new List<string>();
            var k = 0;
            foreach (var key in kind.KeyColumns)
            {
                var value = row.Keys.GetValueOrDefault(key);
                if (value is null)
                {
                    where.Add($"{SqlIdentifier.Quote(key)} IS NULL");
                }
                else
                {
                    if (key == "IDX")
                    {
                        p.Add($"k{k}", int.Parse(value, CultureInfo.InvariantCulture), DbType.Int32);
                    }
                    else
                    {
                        p.Add($"k{k}", value, DbType.AnsiString, ParameterDirection.Input, 50);
                    }

                    where.Add($"{SqlIdentifier.Quote(key)} = @k{k}");
                }

                k++;
            }

            total += await conn.ExecuteAsync(new CommandDefinition(
                $"UPDATE {TableOf(kind)} SET {string.Join(", ", set)} WHERE {string.Join(" AND ", where)}", p, tx,
                commandTimeout: O.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return total;
    }
}
