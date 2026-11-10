using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;

namespace Priston.Infrastructure.Persistence;

internal sealed class LevelSnapshotStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : ILevelSnapshotStore
{
    private int Timeout => options.Value.CommandTimeoutSeconds;

    public async Task<bool> ExistsAsync(DateOnly day, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM dbo.LevelSnapshotRun WHERE SnapshotDate = @day",
            new { day = day.ToDateTime(TimeOnly.MinValue) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false) is not null;
    }

    public async Task SaveAsync(DateOnly day, DateTimeOffset takenUtc, IReadOnlyList<CharacterLevelRow> rows, CancellationToken ct)
    {
        // A LevelTable não tem chave: se um nome aparecer duas vezes, fica a linha de maior nível.
        var unique = rows
            .Where(r => r.Name.Length <= 32)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.Level).First())
            .ToList();

        using var table = new DataTable();
        table.Columns.Add("SnapshotDate", typeof(DateTime));
        table.Columns.Add("CharacterName", typeof(string));
        table.Columns.Add("ClassCode", typeof(int));
        table.Columns.Add("Level", typeof(int));
        var date = day.ToDateTime(TimeOnly.MinValue);
        foreach (var r in unique)
        {
            table.Rows.Add(date, r.Name, r.ClassCode, r.Level);
        }

        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.LevelSnapshotRun (SnapshotDate, TakenUtc, CharacterCount) VALUES (@date, @takenUtc, @count)",
            new { date, takenUtc = takenUtc.UtcDateTime, count = unique.Count }, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);

        using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, tx) { DestinationTableName = "dbo.LevelSnapshot", BulkCopyTimeout = Timeout })
        {
            foreach (DataColumn c in table.Columns)
            {
                bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            }

            await bulk.WriteToServerAsync(table, ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<(DateOnly Date, IReadOnlyDictionary<string, int> Levels)?> FirstOnOrAfterAsync(DateOnly from, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var first = await conn.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT MIN(SnapshotDate) FROM dbo.LevelSnapshotRun WHERE SnapshotDate >= @from",
            new { from = from.ToDateTime(TimeOnly.MinValue) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        if (first is null)
        {
            return null;
        }

        var rows = await conn.QueryAsync<(string CharacterName, int Level)>(new CommandDefinition(
            "SELECT CharacterName, Level FROM dbo.LevelSnapshot WHERE SnapshotDate = @first",
            new { first }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        var levels = rows.ToDictionary(r => r.CharacterName, r => r.Level, StringComparer.OrdinalIgnoreCase);
        return (DateOnly.FromDateTime(first.Value), levels);
    }

    public async Task<DateOnly?> FirstDateAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var first = await conn.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT MIN(SnapshotDate) FROM dbo.LevelSnapshotRun", commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return first is null ? null : DateOnly.FromDateTime(first.Value);
    }

    public async Task<int> DeleteBeforeAsync(DateOnly day, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.LevelSnapshotRun WHERE SnapshotDate < @day",
            new { day = day.ToDateTime(TimeOnly.MinValue) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
    }
}
