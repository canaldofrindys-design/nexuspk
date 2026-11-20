using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.GameAdmin;
using Priston.GameDatabaseIntegration.Connection;

namespace Priston.GameDatabaseIntegration.Admin;

/// <summary>Lê o schema real dos bancos do jogo (sys.databases, sys.tables, sys.columns), sem tocar nos dados.</summary>
internal sealed class GameSchemaReader(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IGameSchemaReader
{
    private int Timeout => options.Value.CommandTimeoutSeconds;

    public bool Enabled => factory.CanAdmin;

    public async Task<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name",
            commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<GameTableInfo>> ListTablesAsync(string database, CancellationToken ct)
    {
        var db = SqlIdentifier.Quote(database);
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<(string Name, long Rows)>(new CommandDefinition(
            $"""
            SELECT t.name, CAST(COALESCE(SUM(p.rows), 0) AS bigint)
            FROM {db}.sys.tables t
            JOIN {db}.sys.schemas s ON s.schema_id = t.schema_id
            LEFT JOIN {db}.sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
            WHERE s.name = 'dbo' AND t.is_ms_shipped = 0
            GROUP BY t.name ORDER BY t.name
            """, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new GameTableInfo(r.Name, r.Rows)).ToList();
    }

    public async Task<IReadOnlyList<GameColumn>> GetColumnsAsync(string database, string table, CancellationToken ct)
    {
        var db = SqlIdentifier.Quote(database);
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<ColumnRow>(new CommandDefinition(
            $"""
            SELECT c.name AS Name, ty.name AS SqlType, CAST(c.max_length AS int) AS MaxLength, CAST(c.precision AS int) AS Precision, CAST(c.scale AS int) AS Scale,
                   c.is_nullable AS Nullable, c.is_identity AS IsIdentity, c.is_computed AS Computed,
                   CAST(CASE WHEN c.default_object_id <> 0 THEN 1 ELSE 0 END AS bit) AS HasDefault
            FROM {db}.sys.columns c
            JOIN {db}.sys.tables t ON t.object_id = c.object_id
            JOIN {db}.sys.schemas s ON s.schema_id = t.schema_id
            JOIN {db}.sys.types ty ON ty.user_type_id = c.system_type_id
            WHERE s.name = 'dbo' AND t.name = @table
            ORDER BY c.column_id
            """, new { table }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new GameColumn(r.Name, r.SqlType, r.MaxLength, r.Precision, r.Scale, r.Nullable, r.IsIdentity, r.Computed, r.HasDefault)).ToList();
    }

    private sealed class ColumnRow
    {
        public string Name { get; init; } = "";
        public string SqlType { get; init; } = "";
        public int MaxLength { get; init; }
        public int Precision { get; init; }
        public int Scale { get; init; }
        public bool Nullable { get; init; }
        public bool IsIdentity { get; init; }
        public bool Computed { get; init; }
        public bool HasDefault { get; init; }
    }
}

/// <summary>
/// Leitura e escrita nas tabelas ligadas pelo painel. Todo nome de banco, tabela e coluna já foi conferido no schema
/// (ResolvedTable) e ainda passa pelo SqlIdentifier; todo valor vai como parâmetro com o tipo da coluna.
/// </summary>
internal sealed class GameTableStore(IGameDbConnectionFactory factory, IOptions<GameDatabaseOptions> options) : IGameTableStore
{
    private int Timeout => options.Value.CommandTimeoutSeconds;

    private static string Name(ResolvedTable t) => $"{SqlIdentifier.Quote(t.Database)}.[dbo].{SqlIdentifier.Quote(t.Table)}";

    private static string Cols(IEnumerable<GameColumn> cols) => string.Join(", ", cols.Select(c => SqlIdentifier.Quote(c.Name)));

    internal static DbType TypeOf(GameColumn c) => c.SqlType switch
    {
        "tinyint" => DbType.Byte,
        "smallint" => DbType.Int16,
        "int" => DbType.Int32,
        "bigint" => DbType.Int64,
        "decimal" or "numeric" => DbType.Decimal,
        "money" or "smallmoney" => DbType.Currency,
        "float" => DbType.Double,
        "real" => DbType.Single,
        "bit" => DbType.Boolean,
        "char" => DbType.AnsiStringFixedLength,
        "varchar" => DbType.AnsiString,
        "nchar" => DbType.StringFixedLength,
        "nvarchar" => DbType.String,
        "date" => DbType.Date,
        "datetime2" => DbType.DateTime2,
        _ => DbType.DateTime,
    };

    private static void Add(DynamicParameters p, string name, GameColumn c, object? value)
    {
        var type = TypeOf(c);
        int? size = type is DbType.AnsiString or DbType.String or DbType.AnsiStringFixedLength or DbType.StringFixedLength
            ? (c.MaxLength < 0 ? -1 : c.MaxChars)
            : null;
        p.Add(name, value, type, ParameterDirection.Input, size);
    }

    /// <summary>Monta "col = @k0 AND col2 IS NULL ..." para as chaves.</summary>
    private static string KeyWhere(ResolvedTable t, IReadOnlyDictionary<string, object?> keys, DynamicParameters p)
    {
        var parts = new List<string>();
        var i = 0;
        foreach (var col in t.KeyColumns)
        {
            var value = keys[col.Name];
            if (value is null)
            {
                parts.Add($"{SqlIdentifier.Quote(col.Name)} IS NULL");
            }
            else
            {
                Add(p, $"k{i}", col, value);
                parts.Add($"{SqlIdentifier.Quote(col.Name)} = @k{i}");
            }

            i++;
        }

        return string.Join(" AND ", parts);
    }

    public async Task<GameRowPage> QueryAsync(ResolvedTable table, IReadOnlyList<GameTableFilter> filters, int skip, int take, CancellationToken ct)
    {
        var p = new DynamicParameters();
        var where = new List<string>();
        for (var i = 0; i < filters.Count; i++)
        {
            var f = filters[i];
            var col = SqlIdentifier.Quote(f.Column.Name);
            if (f.Prefix && f.Value is string s)
            {
                var like = s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
                    .Replace("_", "\\_", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal) + "%";
                p.Add($"f{i}", like, f.Column.SqlType is "nchar" or "nvarchar" ? DbType.String : DbType.AnsiString, ParameterDirection.Input, Math.Max(like.Length, 1));
                where.Add($"{col} LIKE @f{i} ESCAPE '\\'");
            }
            else
            {
                Add(p, $"f{i}", f.Column, f.Value);
                where.Add($"{col} = @f{i}");
            }
        }

        var order = (table.KeyColumns.Count > 0 ? table.KeyColumns : table.Columns.Take(1))
            .Select(c => SqlIdentifier.Quote(c.Name) + (table.NewestFirst ? " DESC" : " ASC"));
        var cols = Cols(table.Columns);
        p.Add("skip", skip);
        p.Add("end", skip + take + 1);
        var sql = $"""
            SELECT {cols} FROM (
                SELECT {cols}, ROW_NUMBER() OVER (ORDER BY {string.Join(", ", order)}) AS [__rn]
                FROM {Name(table)} {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ) x WHERE [__rn] > @skip AND [__rn] <= @end ORDER BY [__rn]
            """;
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        var rows = (await conn.QueryAsync(new CommandDefinition(sql, p, commandTimeout: Timeout * 2, cancellationToken: ct)).ConfigureAwait(false))
            .Cast<IDictionary<string, object?>>()
            .Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(r, StringComparer.Ordinal))
            .ToList();
        var hasMore = rows.Count > take;
        return new GameRowPage(hasMore ? rows.Take(take).ToList() : rows, hasMore);
    }

    public async Task<RowWrite> UpdateAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        var (outcome, before, where, p) = await LockOneAsync(conn, tx, table, keys, ct).ConfigureAwait(false);
        if (outcome != RowWriteOutcome.Ok)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return new RowWrite(outcome, null);
        }

        var set = new List<string>();
        var i = 0;
        foreach (var (name, value) in values)
        {
            Add(p, $"v{i}", table.Column(name), value);
            set.Add($"{SqlIdentifier.Quote(name)} = @v{i++}");
        }

        await conn.ExecuteAsync(new CommandDefinition($"UPDATE {Name(table)} SET {string.Join(", ", set)} WHERE {where}", p, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new RowWrite(RowWriteOutcome.Ok, before);
    }

    public async Task<bool> InsertAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> values, IReadOnlyDictionary<string, object?>? uniqueKeys, CancellationToken ct)
    {
        var p = new DynamicParameters();
        var names = new List<string>();
        var args = new List<string>();
        var i = 0;
        foreach (var (name, value) in values)
        {
            Add(p, $"v{i}", table.Column(name), value);
            names.Add(SqlIdentifier.Quote(name));
            args.Add($"@v{i++}");
        }

        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        if (uniqueKeys is { Count: > 0 })
        {
            var kp = new DynamicParameters();
            var where = KeyWhere(table, uniqueKeys, kp);
            var exists = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                $"SELECT TOP (1) 1 FROM {Name(table)} WITH (UPDLOCK, HOLDLOCK) WHERE {where}", kp, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);
            if (exists is not null)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return false;
            }
        }

        await conn.ExecuteAsync(new CommandDefinition($"INSERT INTO {Name(table)} ({string.Join(", ", names)}) VALUES ({string.Join(", ", args)})", p,
            tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<RowWrite> DeleteAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> keys, CancellationToken ct)
    {
        await using var conn = await factory.OpenAdminAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        var (outcome, before, where, p) = await LockOneAsync(conn, tx, table, keys, ct).ConfigureAwait(false);
        if (outcome != RowWriteOutcome.Ok)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return new RowWrite(outcome, null);
        }

        await conn.ExecuteAsync(new CommandDefinition($"DELETE FROM {Name(table)} WHERE {where}", p, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new RowWrite(RowWriteOutcome.Ok, before);
    }

    /// <summary>Trava as linhas com as chaves dadas e confere que é exatamente uma.</summary>
    private async Task<(RowWriteOutcome, IReadOnlyDictionary<string, object?>?, string, DynamicParameters)> LockOneAsync(
        SqlConnection conn, SqlTransaction tx, ResolvedTable table, IReadOnlyDictionary<string, object?> keys, CancellationToken ct)
    {
        var p = new DynamicParameters();
        var where = KeyWhere(table, keys, p);
        var rows = (await conn.QueryAsync(new CommandDefinition(
            $"SELECT TOP (2) {Cols(table.Columns)} FROM {Name(table)} WITH (UPDLOCK, HOLDLOCK) WHERE {where}", p, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false))
            .Cast<IDictionary<string, object?>>()
            .ToList();
        return rows.Count switch
        {
            0 => (RowWriteOutcome.NotFound, null, where, p),
            > 1 => (RowWriteOutcome.Ambiguous, null, where, p),
            _ => (RowWriteOutcome.Ok, new Dictionary<string, object?>(rows[0], StringComparer.Ordinal), where, p),
        };
    }
}
