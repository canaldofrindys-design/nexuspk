using System.Text.Json;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

/// <summary>Um ranking que pode ser zerado: tabela confirmada, colunas zeradas e colunas que identificam cada linha.</summary>
public sealed record RankingResetKind(string Key, string Label, string Description, string Table, IReadOnlyList<string> KeyColumns, IReadOnlyList<string> ResetColumns);

public static class RankingResetKinds
{
    public static readonly IReadOnlyList<RankingResetKind> All =
    [
        new("pvp-semanal", "PvP semanal", "Zera os BPs da semana (UserDB.BattlePoints.BPsSemanal). Os BPs totais ficam.", "BattlePoints",
            ["Account", "Nick"], ["BPsSemanal"]),
        new("sod-semanal", "SoD semanal", "Zera a pontuação semanal do SoD (UserDB.BattlePoints.SodSemanal).", "BattlePoints",
            ["Account", "Nick"], ["SodSemanal"]),
        new("pvp-geral", "PvP geral", "Zera BPs, DPs, abates, mortes, pontos e o título de Rei do PvP de todos (UserDB.BattlePoints).", "BattlePoints",
            ["Account", "Nick"], ["BPs", "DPs", "BPsSemanal", "KillsPvP", "MortesPvP", "PontosPvP", "IsReiPvP"]),
        new("clas-sod", "Pontos de clã (líder do SoD)", "Zera os pontos de todos os clãs (ClanDB.CL.Cpoint). A home deixa de mostrar líder do SoD até alguém pontuar.", "CL",
            ["IDX"], ["Cpoint"]),
    ];

    public static RankingResetKind? Find(string? key) => All.FirstOrDefault(k => k.Key == key);
}

/// <summary>Valores de uma linha antes do reset, para poder desfazer.</summary>
public sealed record RankingBackupRow(Dictionary<string, string?> Keys, Dictionary<string, long?> Values);

public interface IRankingResetStore
{
    bool Enabled { get; }

    Task<IReadOnlyList<RankingBackupRow>> ReadAsync(RankingResetKind kind, CancellationToken ct);

    /// <summary>Zera as colunas de todas as linhas numa transação. Devolve as linhas alteradas.</summary>
    Task<int> ResetAsync(RankingResetKind kind, CancellationToken ct);

    /// <summary>Devolve os valores guardados, linha a linha, numa transação. Devolve as linhas alteradas.</summary>
    Task<int> RestoreAsync(RankingResetKind kind, IReadOnlyList<RankingBackupRow> rows, CancellationToken ct);
}

public sealed record RankingBackup(long Id, string Kind, string CreatedBy, DateTimeOffset CreatedUtc, int Rows, DateTimeOffset? RestoredUtc, string? RestoredBy);

public interface IRankingBackupStore
{
    Task<long> CreateAsync(string kind, string createdBy, DateTimeOffset now, int rows, string json, CancellationToken ct);

    Task<IReadOnlyList<RankingBackup>> ListAsync(int take, CancellationToken ct);

    Task<(RankingBackup Info, string Json)?> GetAsync(long id, CancellationToken ct);

    Task MarkRestoredAsync(long id, string by, DateTimeOffset now, CancellationToken ct);
}

public sealed record RankingResetRequest(string? Kind, string? Confirm, string? Reason, string? Password);

public sealed record RankingRestoreRequest(string? Reason, string? Password);

/// <summary>
/// Reset de rankings. Antes de zerar, guarda os valores de todas as linhas no banco do portal: o reset pode ser desfeito.
/// Pede a senha do membro e a palavra ZERAR digitada.
/// </summary>
public sealed class RankingResetService(IRankingResetStore game, IRankingBackupStore backups, StaffReauthService reauth, IAuditLog audit, TimeProvider clock)
{
    public const string ConfirmWord = "ZERAR";

    public bool Enabled => game.Enabled;

    public Task<IReadOnlyList<RankingBackup>> ListBackupsAsync(CancellationToken ct) => backups.ListAsync(30, ct);

    public async Task<GameAdminResult> ResetAsync(StaffPrincipal me, RankingResetRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage);
        }

        var kind = RankingResetKinds.Find(r.Kind);
        var errors = new Dictionary<string, string>();
        if (kind is null)
        {
            errors["kind"] = "Escolha o ranking.";
        }

        if (!string.Equals(r.Confirm?.Trim(), ConfirmWord, StringComparison.Ordinal))
        {
            errors["confirm"] = $"Digite {ConfirmWord} para confirmar.";
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        var now = clock.GetUtcNow();
        var rows = await game.ReadAsync(kind!, ct).ConfigureAwait(false);
        var backupId = await backups.CreateAsync(kind!.Key, me.Username, now, rows.Count, JsonSerializer.Serialize(rows), ct).ConfigureAwait(false);
        var changed = await game.ResetAsync(kind, ct).ConfigureAwait(false);
        await GameAudit.WriteAsync(audit, me, "game.ranking.reset", "ranking", kind.Key, client, new { backupId, rows = changed, reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(new { backupId }, $"{kind.Label} zerado ({changed} linha(s)). Cópia de segurança nº {backupId} guardada; o site mostra a mudança em até 5 minutos.");
    }

    public async Task<GameAdminResult> RestoreAsync(StaffPrincipal me, long backupId, RankingRestoreRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(GameAccountAdminService.DisabledMessage);
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            return GameAdminResult.Invalid("reason", reasonError);
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        var found = await backups.GetAsync(backupId, ct).ConfigureAwait(false);
        if (found is null)
        {
            return GameAdminResult.NotFound("Cópia não encontrada.");
        }

        var (info, json) = found.Value;
        if (info.RestoredUtc is not null)
        {
            return GameAdminResult.Conflict("Esta cópia já foi devolvida.");
        }

        var kind = RankingResetKinds.Find(info.Kind);
        var rows = JsonSerializer.Deserialize<List<RankingBackupRow>>(json) ?? [];
        if (kind is null)
        {
            return GameAdminResult.Conflict("Tipo de ranking desconhecido nesta cópia.");
        }

        var changed = await game.RestoreAsync(kind, rows, ct).ConfigureAwait(false);
        await backups.MarkRestoredAsync(backupId, me.Username, clock.GetUtcNow(), ct).ConfigureAwait(false);
        await GameAudit.WriteAsync(audit, me, "game.ranking.restored", "ranking", kind.Key, client, new { backupId, rows = changed, reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, $"Valores da cópia nº {backupId} devolvidos ({changed} linha(s)). Pontos feitos depois do reset foram substituídos pelos da cópia.");
    }
}
