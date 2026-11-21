using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

public sealed record SourceConfigRequest(
    string? Database,
    string? Table,
    IReadOnlyList<string>? KeyColumns,
    IReadOnlyList<string>? EditableColumns,
    IReadOnlyList<string>? SearchColumns,
    string? AccountColumn,
    bool AllowInsert,
    bool AllowDelete,
    bool NewestFirst,
    string? Password);

public sealed record PasswordRequest(string? Password, string? Reason);

public sealed record RowUpdateRequest(Dictionary<string, string?>? Keys, Dictionary<string, string?>? Values, string? Reason);

public sealed record RowInsertRequest(Dictionary<string, string?>? Values, string? Reason);

public sealed record RowDeleteRequest(Dictionary<string, string?>? Keys, string? Reason, string? Password);

public sealed record FunctionSummary(string Key, string Label, string Description, string Mode, bool Configured, string? Source, bool CanWrite, bool CanConfigure);

public sealed record ColumnView(string Name, string Type, int MaxChars, bool Nullable, bool Identity, bool Computed, bool HasDefault, bool Supported, bool Insertable,
    bool IsKey, bool IsEditable, bool IsSearch, bool IsAccount);

public sealed record BrowseView(
    FunctionSummary Function,
    GameSourceBinding Binding,
    IReadOnlyList<ColumnView> Columns,
    IReadOnlyList<Dictionary<string, string?>> Rows,
    int Page,
    bool HasMore,
    bool CanEdit,
    bool CanInsert,
    bool CanDelete);

public sealed record TableSuggestion(string Name, long Rows, bool Suggested);

/// <summary>
/// Editores das tabelas do jogo que ainda não estão mapeadas (Box Donate, lojas, drops, NPC, catálogo, logs de itens).
/// O superadministrador liga cada função à tabela real; o painel confere banco, tabela e colunas no schema a cada uso
/// e só monta SQL com esses nomes conferidos. Valores sempre vão como parâmetros.
/// </summary>
public sealed partial class GameTableService(
    IGameSchemaReader schema,
    IGameTableStore tables,
    IGameSourceBindingStore bindings,
    IGameAccountStore accounts,
    StaffReauthService reauth,
    IAuditLog audit,
    IMemoryCache cache,
    TimeProvider clock)
{
    public const int PageSize = 50;
    public const string NotConfigured = "Esta função ainda não foi ligada a uma tabela do jogo. Peça ao superadministrador para abrir \"Configurar\".";

    [GeneratedRegex("^[A-Za-z0-9_]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();

    public static bool IsSafeName(string? name) => name is not null && SafeName().IsMatch(name);

    public bool Enabled => schema.Enabled;

    public async Task<IReadOnlyList<FunctionSummary>> ListAsync(StaffPrincipal me, CancellationToken ct)
    {
        var all = await bindings.ListAsync(ct).ConfigureAwait(false);
        return GameFunctions.All
            .Where(f => me.Can(f.ReadCapability) || me.Can(f.WriteCapability))
            .Select(f => Summary(me, f, all.FirstOrDefault(b => b.FunctionKey == f.Key) ?? GameFunctions.BuiltIn.GetValueOrDefault(f.Key)))
            .ToList();
    }

    private static FunctionSummary Summary(StaffPrincipal me, GameFunction f, GameSourceBinding? b) =>
        new(f.Key, f.Label, f.Description, f.Mode.ToString().ToLowerInvariant(), b is not null, b is null ? null : $"{b.Database}.dbo.{b.Table}",
            f.Mode != GameFunctionMode.ReadOnly && me.Can(f.WriteCapability), me.Can(Capability.ConfigureGameSources));

    // ---------- configuração ----------

    public async Task<GameAdminResult> DatabasesAsync(CancellationToken ct)
    {
        if (!schema.Enabled)
        {
            return GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage);
        }

        var dbs = await schema.ListDatabasesAsync(ct).ConfigureAwait(false);
        return GameAdminResult.Ok(dbs.Where(IsSafeName).ToList());
    }

    public async Task<GameAdminResult> TablesAsync(string functionKey, string? database, CancellationToken ct)
    {
        var f = GameFunctions.Find(functionKey);
        if (f is null)
        {
            return GameAdminResult.NotFound();
        }

        if (!schema.Enabled)
        {
            return GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage);
        }

        if (!IsSafeName(database) || !(await schema.ListDatabasesAsync(ct).ConfigureAwait(false)).Contains(database!, StringComparer.OrdinalIgnoreCase))
        {
            return GameAdminResult.Invalid("database", "Banco não encontrado.");
        }

        var list = await schema.ListTablesAsync(database!, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(list
            .Where(t => IsSafeName(t.Name))
            .Select(t => new TableSuggestion(t.Name, t.Rows, f.Keywords.Any(k => t.Name.Contains(k, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(t => t.Suggested).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public async Task<GameAdminResult> ColumnsAsync(string? database, string? table, CancellationToken ct)
    {
        if (!schema.Enabled)
        {
            return GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage);
        }

        if (!IsSafeName(database) || !IsSafeName(table))
        {
            return GameAdminResult.Invalid("table", "Tabela inválida.");
        }

        var cols = await schema.GetColumnsAsync(database!, table!, ct).ConfigureAwait(false);
        return cols.Count == 0
            ? GameAdminResult.NotFound("Tabela não encontrada.")
            : GameAdminResult.Ok(cols.Select(c => View(c, null)).ToList());
    }

    public Task<GameSourceBinding?> GetBindingAsync(string functionKey, CancellationToken ct) => EffectiveBindingAsync(functionKey, ct);

    /// <summary>A configuração salva pelo superadministrador; sem ela, a tabela que o servidor já usa (se houver).</summary>
    private async Task<GameSourceBinding?> EffectiveBindingAsync(string functionKey, CancellationToken ct) =>
        await bindings.GetAsync(functionKey, ct).ConfigureAwait(false) ?? GameFunctions.BuiltIn.GetValueOrDefault(functionKey);

    public async Task<GameAdminResult> SaveConfigAsync(StaffPrincipal me, string functionKey, SourceConfigRequest r, ClientInfo client, CancellationToken ct)
    {
        var f = GameFunctions.Find(functionKey);
        if (f is null)
        {
            return GameAdminResult.NotFound();
        }

        if (!schema.Enabled)
        {
            return GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage);
        }

        if (!IsSafeName(r.Database) || !IsSafeName(r.Table))
        {
            return GameAdminResult.Invalid("table", "Escolha o banco e a tabela.");
        }

        var cols = await schema.GetColumnsAsync(r.Database!, r.Table!, ct).ConfigureAwait(false);
        if (cols.Count == 0)
        {
            return GameAdminResult.Invalid("table", "Tabela não encontrada.");
        }

        var byName = cols.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var errors = new Dictionary<string, string>();
        List<string> Pick(IReadOnlyList<string>? names, string field, Func<GameColumn, string?> check)
        {
            var list = (names ?? []).Distinct(StringComparer.Ordinal).ToList();
            foreach (var n in list)
            {
                if (!byName.TryGetValue(n, out var c) || !IsSafeName(n))
                {
                    errors[field] = $"A coluna {n} não existe nesta tabela.";
                }
                else if (check(c) is { } problem)
                {
                    errors[field] = $"{n}: {problem}";
                }
            }

            return list;
        }

        var keys = Pick(r.KeyColumns, "keyColumns", c =>
            !c.Supported ? "tipo não suportado como chave" : GameValues.Kind(c.SqlType) == GameValueKind.ApproxNumber ? "número com vírgula flutuante não serve de chave" : null);
        var editable = Pick(r.EditableColumns, "editableColumns", c =>
            !c.Supported ? "tipo não editável" : c.Identity || c.Computed ? "coluna automática não pode ser editada" : null);
        var search = Pick(r.SearchColumns, "searchColumns", c =>
            GameValues.Kind(c.SqlType) is GameValueKind.Text or GameValueKind.WholeNumber ? null : "só texto ou número inteiro servem para busca");
        string? account = null;
        if (!string.IsNullOrEmpty(r.AccountColumn))
        {
            account = Pick([r.AccountColumn], "accountColumn", c => GameValues.Kind(c.SqlType) == GameValueKind.Text ? null : "a coluna da conta precisa ser texto").FirstOrDefault();
        }

        var needsKeys = f.Mode == GameFunctionMode.Edit && (editable.Count > 0 || r.AllowDelete);
        if (needsKeys && keys.Count == 0)
        {
            errors["keyColumns"] = "Marque a(s) coluna(s) que identificam cada linha (ex.: código do item). Sem isso o painel não sabe qual linha mudar.";
        }

        if (f.Mode == GameFunctionMode.Insert && account is null)
        {
            errors["accountColumn"] = "Marque a coluna que recebe o login da conta.";
        }

        if (f.Mode == GameFunctionMode.Insert && cols.Any(c => !c.Nullable && !c.HasDefault && !c.Identity && !c.Computed && !c.Supported))
        {
            errors["table"] = "Esta tabela tem coluna obrigatória de um tipo que o painel não preenche.";
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        var before = await bindings.GetAsync(f.Key, ct).ConfigureAwait(false);
        var binding = new GameSourceBinding(f.Key, r.Database!, r.Table!, keys,
            f.Mode == GameFunctionMode.Edit ? editable : [], search, account,
            f.Mode == GameFunctionMode.Insert || (f.Mode == GameFunctionMode.Edit && r.AllowInsert),
            f.Mode == GameFunctionMode.Edit && r.AllowDelete, r.NewestFirst, me.Username, clock.GetUtcNow());
        await bindings.SaveAsync(binding, ct).ConfigureAwait(false);
        cache.Remove(CacheKey(binding.Database, binding.Table));
        await GameAudit.WriteAsync(audit, me, "game.source.configured", "game-function", f.Key, client, new { before, after = binding }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, $"{f.Label} ligado a {binding.Database}.dbo.{binding.Table}.");
    }

    public async Task<GameAdminResult> RemoveConfigAsync(StaffPrincipal me, string functionKey, PasswordRequest r, ClientInfo client, CancellationToken ct)
    {
        var f = GameFunctions.Find(functionKey);
        if (f is null || await bindings.GetAsync(f.Key, ct).ConfigureAwait(false) is not { } before)
        {
            return GameAdminResult.NotFound();
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        await bindings.DeleteAsync(f.Key, ct).ConfigureAwait(false);
        await GameAudit.WriteAsync(audit, me, "game.source.removed", "game-function", f.Key, client, new { before }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, $"{f.Label} desligado da tabela.");
    }

    // ---------- uso ----------

    public async Task<GameAdminResult> BrowseAsync(StaffPrincipal me, string functionKey, IReadOnlyDictionary<string, string> query, int page, CancellationToken ct)
    {
        var (f, binding, table, error) = await OpenAsync(me, functionKey, write: false, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        var filters = new List<GameTableFilter>();
        var errors = new Dictionary<string, string>();
        foreach (var name in binding!.SearchColumns)
        {
            if (!query.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var col = table!.Column(name);
            var exact = raw.StartsWith('=');
            var text = exact ? raw[1..] : raw;
            if (!GameValues.TryParse(col with { Nullable = false }, text.Trim(), out var v, out var e))
            {
                errors[name] = e!;
                continue;
            }

            filters.Add(new GameTableFilter(col, v!, Prefix: !exact && GameValues.Kind(col.SqlType) == GameValueKind.Text));
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        page = Math.Clamp(page, 1, 10_000);
        var result = await tables.QueryAsync(table!, filters, (page - 1) * PageSize, PageSize, ct).ConfigureAwait(false);
        var canWrite = f!.Mode != GameFunctionMode.ReadOnly && me.Can(f.WriteCapability);
        return GameAdminResult.Ok(new BrowseView(
            Summary(me, f, binding),
            binding,
            table!.Columns.Select(c => View(c, binding)).ToList(),
            result.Rows.Select(r => r.ToDictionary(kv => kv.Key, kv => GameValues.Format(kv.Value))).ToList(),
            page,
            result.HasMore,
            canWrite && f.Mode == GameFunctionMode.Edit && binding.EditableColumns.Count > 0 && binding.KeyColumns.Count > 0,
            canWrite && binding.AllowInsert,
            canWrite && binding.AllowDelete && binding.KeyColumns.Count > 0));
    }

    public async Task<GameAdminResult> UpdateRowAsync(StaffPrincipal me, string functionKey, RowUpdateRequest r, ClientInfo client, CancellationToken ct)
    {
        var (f, binding, table, error) = await OpenAsync(me, functionKey, write: true, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        if (f!.Mode != GameFunctionMode.Edit || binding!.EditableColumns.Count == 0 || binding.KeyColumns.Count == 0)
        {
            return GameAdminResult.Invalid("values", "Esta função não permite editar.");
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        var errors = new Dictionary<string, string>();
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        var keys = ParseKeys(table!, r.Keys, errors);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in binding.EditableColumns)
        {
            if (r.Values is null || !r.Values.TryGetValue(name, out var raw))
            {
                continue;
            }

            if (GameValues.TryParse(table!.Column(name), raw, out var v, out var e))
            {
                values[name] = v;
            }
            else
            {
                errors[name] = e!;
            }
        }

        if (values.Count == 0 && !errors.ContainsKey("values"))
        {
            errors["values"] = "Nada para salvar.";
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        var res = await tables.UpdateAsync(table!, keys, values, ct).ConfigureAwait(false);
        if (res.Outcome != RowWriteOutcome.Ok)
        {
            return RowConflict(res.Outcome);
        }

        await GameAudit.WriteAsync(audit, me, $"game.{f.Key}.updated", "game-row", KeyText(binding, keys), client,
            new { table = $"{binding.Database}.dbo.{binding.Table}", keys = Fmt(keys), before = Fmt(res.Before!, values.Keys), after = Fmt(values), reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, "Linha salva. O jogo pode precisar recarregar a configuração (ou reiniciar) para usar o valor novo.");
    }

    public async Task<GameAdminResult> InsertRowAsync(StaffPrincipal me, string functionKey, RowInsertRequest r, ClientInfo client, CancellationToken ct)
    {
        var (f, binding, table, error) = await OpenAsync(me, functionKey, write: true, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        if (!binding!.AllowInsert)
        {
            return GameAdminResult.Invalid("values", "Esta função não permite incluir.");
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        var errors = new Dictionary<string, string>();
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var col in table!.Columns.Where(c => c.Insertable))
        {
            string? raw = null;
            r.Values?.TryGetValue(col.Name, out raw);
            if (string.IsNullOrEmpty(raw) && col.HasDefault)
            {
                continue; // vale o padrão da tabela
            }

            if (GameValues.TryParse(col, raw, out var v, out var e))
            {
                values[col.Name] = v;
            }
            else
            {
                errors[col.Name] = e!;
            }
        }

        if (binding.AccountColumn is { } accountCol && errors.Count == 0)
        {
            var login = (values.GetValueOrDefault(accountCol) as string)?.Trim();
            if (!GameAccountAdminService.IsValidLogin(login) || !await accounts.ExistsAsync(login!, ct).ConfigureAwait(false))
            {
                errors[accountCol] = "Conta não encontrada no jogo.";
            }
            else
            {
                values[accountCol] = login;
            }
        }

        if (values.Count == 0 && errors.Count == 0)
        {
            errors["values"] = "Preencha os campos.";
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        // No modo edição a chave identifica a linha: não deixa nascer uma segunda linha com a mesma chave.
        // No envio (Box Donate) cada envio é uma linha nova, então repetir é normal.
        IReadOnlyDictionary<string, object?>? uniqueKeys = null;
        if (f!.Mode == GameFunctionMode.Edit && binding.KeyColumns.All(values.ContainsKey))
        {
            uniqueKeys = binding.KeyColumns.ToDictionary(k => k, k => values[k], StringComparer.Ordinal);
        }

        if (!await tables.InsertAsync(table, values, uniqueKeys, ct).ConfigureAwait(false))
        {
            return GameAdminResult.Conflict("Já existe uma linha com essa mesma chave. Edite a linha que já existe em vez de incluir outra.");
        }

        var target = binding.AccountColumn is { } a ? (string)values[a]! : $"{binding.Database}.dbo.{binding.Table}";
        await GameAudit.WriteAsync(audit, me, $"game.{f.Key}.inserted", binding.AccountColumn is null ? "game-row" : "game-account", target, client,
            new { table = $"{binding.Database}.dbo.{binding.Table}", values = Fmt(values), reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, f.Mode == GameFunctionMode.Insert ? $"Enviado para {target}." : "Linha incluída.");
    }

    public async Task<GameAdminResult> DeleteRowAsync(StaffPrincipal me, string functionKey, RowDeleteRequest r, ClientInfo client, CancellationToken ct)
    {
        var (f, binding, table, error) = await OpenAsync(me, functionKey, write: true, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        if (!binding!.AllowDelete || binding.KeyColumns.Count == 0)
        {
            return GameAdminResult.Invalid("keys", "Esta função não permite apagar.");
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        var errors = new Dictionary<string, string>();
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        var keys = ParseKeys(table!, r.Keys, errors);
        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        var res = await tables.DeleteAsync(table!, keys, ct).ConfigureAwait(false);
        if (res.Outcome != RowWriteOutcome.Ok)
        {
            return RowConflict(res.Outcome);
        }

        await GameAudit.WriteAsync(audit, me, $"game.{f!.Key}.deleted", "game-row", KeyText(binding, keys), client,
            new { table = $"{binding.Database}.dbo.{binding.Table}", before = Fmt(res.Before!), reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, "Linha apagada. A cópia dos valores ficou na auditoria.");
    }

    // ---------- apoio ----------

    private async Task<(GameFunction? F, GameSourceBinding? B, ResolvedTable? T, GameAdminResult? Error)> OpenAsync(StaffPrincipal me, string functionKey, bool write, CancellationToken ct)
    {
        var f = GameFunctions.Find(functionKey);
        if (f is null)
        {
            return (null, null, null, GameAdminResult.NotFound());
        }

        if (!(write ? me.Can(f.WriteCapability) : me.Can(f.ReadCapability) || me.Can(f.WriteCapability)))
        {
            return (f, null, null, new GameAdminResult(GameAdminStatus.Forbidden, "Sua função não permite esta ação."));
        }

        if (write && f.Mode == GameFunctionMode.ReadOnly)
        {
            return (f, null, null, GameAdminResult.Invalid("values", "Esta função é só de consulta."));
        }

        if (!schema.Enabled)
        {
            return (f, null, null, GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage));
        }

        var b = await EffectiveBindingAsync(f.Key, ct).ConfigureAwait(false);
        if (b is null)
        {
            return (f, null, null, GameAdminResult.Disabled(NotConfigured));
        }

        var table = await ResolveAsync(b, ct).ConfigureAwait(false);
        return table is null
            ? (f, b, null, GameAdminResult.Disabled($"A tabela {b.Database}.dbo.{b.Table} mudou ou sumiu (alguma coluna configurada não existe mais). Abra \"Configurar\" de novo."))
            : (f, b, table, null);
    }

    internal async Task<ResolvedTable?> ResolveAsync(GameSourceBinding b, CancellationToken ct)
    {
        if (!IsSafeName(b.Database) || !IsSafeName(b.Table))
        {
            return null;
        }

        var cols = await cache.GetOrCreateAsync(CacheKey(b.Database, b.Table), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await schema.GetColumnsAsync(b.Database, b.Table, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (cols is null || cols.Count == 0)
        {
            return null;
        }

        var safe = cols.Where(c => IsSafeName(c.Name) && c.Supported).ToList();
        bool Has(string n) => safe.Any(c => string.Equals(c.Name, n, StringComparison.Ordinal));
        var named = b.KeyColumns.Concat(b.EditableColumns).Concat(b.SearchColumns).Concat(b.AccountColumn is null ? [] : [b.AccountColumn]);
        if (!named.All(Has))
        {
            return null;
        }

        return new ResolvedTable(b.Database, b.Table, safe, b.KeyColumns.Select(k => safe.First(c => c.Name == k)).ToList(), b.NewestFirst);
    }

    private static Dictionary<string, object?> ParseKeys(ResolvedTable table, Dictionary<string, string?>? raw, Dictionary<string, string> errors)
    {
        var keys = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var col in table.KeyColumns)
        {
            if (raw is null || !raw.TryGetValue(col.Name, out var text))
            {
                errors["keys"] = "Linha não identificada. Recarregue a página.";
                continue;
            }

            // Na chave, NULL é NULL (a linha original tinha NULL) e texto vazio é texto vazio.
            if (text is null)
            {
                keys[col.Name] = null;
            }
            else if (GameValues.TryParse(col with { Nullable = false }, text, out var v, out _))
            {
                keys[col.Name] = v;
            }
            else
            {
                errors["keys"] = "Linha não identificada. Recarregue a página.";
            }
        }

        return keys;
    }

    private static GameAdminResult RowConflict(RowWriteOutcome o) => o == RowWriteOutcome.NotFound
        ? GameAdminResult.Conflict("A linha não existe mais (alguém mudou ou apagou). Recarregue a página.")
        : GameAdminResult.Conflict("Mais de uma linha tem essa mesma chave, então nada foi mudado. Marque mais colunas como chave em \"Configurar\".");

    private static ColumnView View(GameColumn c, GameSourceBinding? b) =>
        new(c.Name, c.SqlType, c.MaxChars == int.MaxValue ? -1 : c.MaxChars, c.Nullable, c.Identity, c.Computed, c.HasDefault, c.Supported, c.Insertable,
            b?.KeyColumns.Contains(c.Name) ?? false, b?.EditableColumns.Contains(c.Name) ?? false, b?.SearchColumns.Contains(c.Name) ?? false,
            b?.AccountColumn == c.Name);

    private static string KeyText(GameSourceBinding b, IReadOnlyDictionary<string, object?> keys) =>
        $"{b.Table}:" + string.Join(",", keys.Select(k => $"{k.Key}={GameValues.Format(k.Value)}"));

    private static Dictionary<string, string?> Fmt(IReadOnlyDictionary<string, object?> values, IEnumerable<string>? only = null)
    {
        var set = only?.ToHashSet(StringComparer.Ordinal);
        return values.Where(kv => set is null || set.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => GameValues.Format(kv.Value));
    }

    private static string CacheKey(string db, string table) => $"game-columns:{db}:{table}";
}
