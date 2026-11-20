using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Domain.Characters;

namespace Priston.Application.GameAdmin;

public sealed record BanRequest(string? Reason, int? Days, string? Password);

public sealed record UnbanRequest(string? Reason);

public sealed record CoinRequest(string? Mode, long? Amount, string? Reason);

public sealed record VipAddRequest(int? Plan, string? Character, int? Days, string? Reason);

public sealed record VipRowRef(string? PlayerName, int? Plan, DateTime? StartLocal, DateTime? EndLocal);

public sealed record VipExtendRequest(VipRowRef? Row, int? Days, string? Reason);

public sealed record VipRemoveRequest(VipRowRef? Row, string? Reason, string? Password);

public sealed record DiagnosticCheck(string Key, string Level, string Title, string Detail);

public sealed record AccountCharacterView(string Name, string ClassName, int? Level, string? Clan);

public sealed record AccountVipView(string? PlayerName, int? Plan, string PlanLabel, DateTime? StartLocal, DateTime? EndLocal, DateTimeOffset? EndsUtc, bool Active);

public sealed record AccountView(
    string Login,
    DateTimeOffset? RegisteredUtc,
    DateTimeOffset? LastDayUtc,
    bool Blocked,
    bool Deleted,
    long? Coins,
    int? GameMasterLevel,
    IReadOnlyList<AccountCharacterView> Characters,
    IReadOnlyList<AccountVipView> Vips,
    IReadOnlyList<GamePvpRow> Pvp,
    PlayerAccount? Portal,
    int ActivePortalSessions,
    IReadOnlyList<GameBanRecord> Bans,
    IReadOnlyList<AuditEntry> Activity,
    IReadOnlyList<DiagnosticCheck> Diagnostics,
    IReadOnlyDictionary<string, string> VipPlans);

/// <summary>
/// Contas do jogo no painel: busca, detalhes, diagnóstico, banimento, coins e VIP.
/// Toda escrita exige motivo e vai para a auditoria com o antes e o depois; banir e tirar VIP pedem a senha do membro.
/// </summary>
public sealed partial class GameAccountAdminService(
    IGameAdminAccountStore game,
    IGameBanStore bans,
    IAccountActivityReader activity,
    IPlayerAccountRepository portalAccounts,
    IPlayerSessionStore playerSessions,
    StaffReauthService reauth,
    IAuditLog audit,
    IOptions<GameAdminOptions> options,
    TimeProvider clock)
{
    public const string DisabledMessage =
        "O login de administração do jogo não está configurado (GameDatabase:AdminConnectionString). Veja docs/ADMIN.md, seção \"Ligar as funções do jogo\".";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();

    [GeneratedRegex("^[\\p{L}\\p{N} _\\-\\[\\]\\.]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex TermPattern();

    public static bool IsValidLogin(string? login) => login is not null && LoginPattern().IsMatch(login);

    private GameAdminOptions O => options.Value;

    public async Task<GameAdminResult> SearchAsync(string? term, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var t = (term ?? string.Empty).Trim();
        if (t.Length < 2 || !TermPattern().IsMatch(t))
        {
            return GameAdminResult.Invalid("q", "Digite pelo menos 2 letras do login ou do nome do personagem.");
        }

        return GameAdminResult.Ok(await game.SearchAsync(t, 50, ct).ConfigureAwait(false));
    }

    public async Task<GameAdminResult> GetAsync(string login, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        if (!IsValidLogin(login))
        {
            return GameAdminResult.NotFound("Conta não encontrada.");
        }

        var snap = await game.GetAsync(login, ct).ConfigureAwait(false);
        if (snap is null)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        var now = clock.GetUtcNow();
        var portal = await portalAccounts.FindAsync(canonical, ct).ConfigureAwait(false);
        var sessions = await activity.CountActivePlayerSessionsAsync(canonical, now, ct).ConfigureAwait(false);
        var banList = await bans.ListAsync(canonical, ct).ConfigureAwait(false);
        var recent = await activity.ListForAccountAsync(canonical, 30, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(BuildView(canonical, snap, portal, sessions, banList, recent, now));
    }

    internal AccountView BuildView(string login, GameAccountSnapshot snap, PlayerAccount? portal, int sessions, IReadOnlyList<GameBanRecord> banList, IReadOnlyList<AuditEntry> recent, DateTimeOffset now)
    {
        var row = snap.AccountRows[0];
        var clanByChar = snap.Clans
            .GroupBy(c => c.CharacterName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ClanName?.Trim(), StringComparer.OrdinalIgnoreCase);
        var characters = snap.Characters
            .Select(c => new AccountCharacterView(c.Name.Trim(), CharacterClassNames.ToPortuguese(CharacterClassNames.FromCode(c.Class ?? 0)), c.Level,
                clanByChar.TryGetValue(c.Name.Trim(), out var clan) ? clan : null))
            .ToList();
        var vips = snap.Vips
            .Select(v =>
            {
                var ends = GameTime.ToUtc(v.EndLocal);
                return new AccountVipView(v.PlayerName?.Trim(), v.Plan, PlanLabel(v.Plan), v.StartLocal, v.EndLocal, ends, ends > now);
            })
            .OrderByDescending(v => v.EndLocal)
            .ToList();
        long? coins = snap.CoinRows.Count == 0 ? null : snap.CoinRows.Sum(c => (long)(c ?? 0));
        return new AccountView(
            login,
            GameTime.ToUtc(row.RegistDay),
            GameTime.ToUtc(row.LastDay),
            IsSet(row.BlockChk),
            IsSet(row.DelChk),
            coins,
            snap.GameMasterLevel,
            characters,
            vips,
            snap.Pvp,
            portal,
            sessions,
            banList,
            recent,
            Diagnose(snap, portal, sessions, banList, now),
            O.VipPlans);
    }

    /// <summary>Checagens da conta em linguagem simples. Nível: ok, info, aviso, problema.</summary>
    internal IReadOnlyList<DiagnosticCheck> Diagnose(GameAccountSnapshot snap, PlayerAccount? portal, int sessions, IReadOnlyList<GameBanRecord> banList, DateTimeOffset now)
    {
        var list = new List<DiagnosticCheck>();
        var row = snap.AccountRows[0];
        void Add(string key, string level, string title, string detail) => list.Add(new(key, level, title, detail));

        if (snap.AccountRows.Count > 1)
        {
            Add("account-duplicate", "problema", "Conta duplicada no jogo",
                $"Existem {snap.AccountRows.Count} linhas com este login na tabela de contas. O jogo pode usar qualquer uma; apague as sobras com cuidado (faça backup antes).");
        }
        else
        {
            Add("account-row", "ok", "Conta encontrada", "Uma linha na tabela de contas do jogo, como deve ser.");
        }

        var activeBan = banList.FirstOrDefault(b => b.LiftedUtc is null);
        if (IsSet(row.DelChk))
        {
            Add("deleted", "problema", "Conta marcada como apagada", "DelChk está marcado: o jogo e o site recusam o login.");
        }

        if (IsSet(row.BlockChk))
        {
            Add("blocked", "aviso", "Conta banida",
                activeBan is null
                    ? "BlockChk está marcado, mas não há banimento registrado pelo painel (foi feito por outro meio). Desbanir pelo painel limpa a marca."
                    : $"Banida por {activeBan.BannedBy}: {activeBan.Reason}" + (activeBan.ExpiresUtc is { } e ? $" Termina em {Fmt(e)}." : " Sem data para acabar."));
        }
        else if (activeBan is not null)
        {
            Add("ban-mismatch", "aviso", "Banimento registrado, mas a conta está liberada",
                "O painel registrou um banimento ativo, mas BlockChk está desmarcado no jogo. Alguém liberou por fora; desbanir pelo painel encerra o registro.");
        }
        else
        {
            Add("not-blocked", "ok", "Sem banimento", "A conta pode entrar no jogo.");
        }

        if (snap.Characters.Count == 0)
        {
            Add("no-characters", "info", "Nenhum personagem", "A conta ainda não tem personagem na LevelTable (normal em conta nova).");
        }
        else
        {
            Add("characters", "ok", "Personagens", $"{snap.Characters.Count} personagem(ns) na LevelTable.");
        }

        var dupNames = snap.Characters.GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupNames.Count > 0)
        {
            Add("character-duplicate", "aviso", "Personagem repetido na LevelTable", "Aparecem mais de uma vez: " + string.Join(", ", dupNames) + ". O ranking usa o maior nível.");
        }

        if (snap.CharactersSharedWithOtherAccounts.Count > 0)
        {
            Add("character-shared", "problema", "Nome de personagem em outra conta",
                "Estes nomes também aparecem em outra conta: " + string.Join(", ", snap.CharactersSharedWithOtherAccounts) + ". Pode causar erro de ranking, clã e VIP.");
        }

        switch (snap.CoinRows.Count)
        {
            case 0:
                Add("coins-none", "info", "Sem registro de coins", "A conta ainda não tem linha em CoinShopDB.UserInfo. O primeiro crédito pelo painel cria a linha.");
                break;
            case > 1:
                Add("coins-duplicate", "problema", "Coins duplicados", $"Existem {snap.CoinRows.Count} linhas de saldo. O painel não muda coins até sobrar uma só.");
                break;
            default:
                if (snap.CoinRows[0] is < 0)
                {
                    Add("coins-negative", "problema", "Saldo de coins negativo", $"Saldo atual: {snap.CoinRows[0]}.");
                }
                else
                {
                    Add("coins", "ok", "Coins", $"Saldo: {(snap.CoinRows[0] ?? 0).ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))}.");
                }

                break;
        }

        var activeVips = snap.Vips.Where(v => GameTime.ToUtc(v.EndLocal) > now).ToList();
        if (activeVips.Count > 1)
        {
            Add("vip-overlap", "aviso", "Mais de um VIP ativo ao mesmo tempo", "Confira se o jogo soma ou usa só um. Pode ser compra repetida.");
        }
        else if (activeVips.Count == 1)
        {
            Add("vip", "ok", "VIP ativo", $"{PlanLabel(activeVips[0].Plan)} até {Fmt(GameTime.ToUtc(activeVips[0].EndLocal)!.Value)}.");
        }
        else
        {
            Add("vip-none", "info", "Sem VIP ativo", snap.Vips.Count > 0 ? "Teve VIP, já vencido." : "Nunca teve VIP.");
        }

        var badVip = snap.Vips.Where(v => v.StartLocal > v.EndLocal || v.EndLocal is null || v.StartLocal is null).ToList();
        if (badVip.Count > 0)
        {
            Add("vip-dates", "aviso", "VIP com data estranha", $"{badVip.Count} registro(s) de VIP sem data ou com fim antes do início.");
        }

        var foreignVip = snap.Vips.Where(v => !string.IsNullOrWhiteSpace(v.PlayerName)
                                              && !snap.Characters.Any(c => string.Equals(c.Name.Trim(), v.PlayerName!.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        if (foreignVip.Count > 0)
        {
            Add("vip-character", "aviso", "VIP em personagem que não está na conta",
                "Personagem do VIP não aparece na LevelTable desta conta: " + string.Join(", ", foreignVip.Select(v => v.PlayerName!.Trim()).Distinct()) + ".");
        }

        if (snap.GameMasterLevel is { } gm)
        {
            Add("gm", "info", "Conta de GM", $"Nível de GM {gm} (UserDB.GameMasters). Fica fora dos rankings.");
        }

        if (portal is null)
        {
            Add("portal-none", "info", "Nunca entrou no site novo", "Não há cadastro web desta conta no portal. Sem e-mail, a recuperação de senha passa pelo suporte.");
        }
        else if (portal.Email is null)
        {
            Add("portal-email", "aviso", "Sem e-mail cadastrado", "O jogador não consegue recuperar a senha sozinho.");
        }
        else if (portal.EmailConfirmedUtc is null)
        {
            Add("portal-unconfirmed", "aviso", "E-mail não confirmado", "O e-mail cadastrado ainda não foi confirmado.");
        }
        else
        {
            Add("portal-ok", "ok", "E-mail confirmado", "O jogador consegue recuperar a senha pelo site.");
        }

        if (sessions > 0)
        {
            Add("portal-sessions", "info", "Sessões abertas no site", $"{sessions} sessão(ões) ativa(s) no painel do jogador.");
        }

        return list;
    }

    public async Task<GameAdminResult> BanAsync(StaffPrincipal me, string login, BanRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var errors = new Dictionary<string, string>();
        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        if (r.Days is < 0 or > 3650)
        {
            errors["days"] = "Use de 1 a 3650 dias, ou deixe vazio para banimento sem prazo.";
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        if (!IsValidLogin(login) || await game.GetAsync(login, ct).ConfigureAwait(false) is not { } snap)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        if (snap.GameMasterLevel is not null && me.Role != StaffRoles.SuperAdmin)
        {
            return GameAdminResult.Invalid("reason", "Conta de GM: só o superadministrador pode banir.");
        }

        var now = clock.GetUtcNow();
        DateTimeOffset? expires = r.Days is > 0 ? now.AddDays(r.Days.Value) : null;
        var changed = await game.SetBlockedAsync(canonical, true, ct).ConfigureAwait(false);
        await bans.LiftActiveAsync(canonical, me.Username, "Substituído por novo banimento.", now, ct).ConfigureAwait(false);
        var id = await bans.CreateAsync(canonical, reason!, me.Username, now, expires, ct).ConfigureAwait(false);
        await playerSessions.RevokeAllAsync(canonical, now, ct).ConfigureAwait(false);
        await GameAudit.WriteAsync(audit, me, "game.account.banned", "game-account", canonical, client,
            new { banId = id, reason, days = r.Days, expiresUtc = expires, rows = changed, before = new { blocked = IsSet(snap.AccountRows[0].BlockChk) } }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, expires is null
            ? "Conta banida sem prazo. Se o jogador estiver online, ele continua até sair do jogo."
            : $"Conta banida até {Fmt(expires.Value)}. Se o jogador estiver online, ele continua até sair do jogo.");
    }

    public async Task<GameAdminResult> UnbanAsync(StaffPrincipal me, string login, UnbanRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            return GameAdminResult.Invalid("reason", reasonError);
        }

        if (!IsValidLogin(login) || await game.GetAsync(login, ct).ConfigureAwait(false) is not { } snap)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        var now = clock.GetUtcNow();
        var changed = await game.SetBlockedAsync(canonical, false, ct).ConfigureAwait(false);
        await bans.LiftActiveAsync(canonical, me.Username, reason!, now, ct).ConfigureAwait(false);
        await GameAudit.WriteAsync(audit, me, "game.account.unbanned", "game-account", canonical, client,
            new { reason, rows = changed, before = new { blocked = IsSet(snap.AccountRows[0].BlockChk) } }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, "Conta liberada.");
    }

    /// <summary>Tira o banimento das contas cujo prazo acabou (Worker, a cada 5 minutos).</summary>
    public async Task<int> LiftExpiredAsync(CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var expired = await bans.ListExpiredActiveAsync(now, ct).ConfigureAwait(false);
        foreach (var b in expired)
        {
            var rows = await game.SetBlockedAsync(b.GameLogin, false, ct).ConfigureAwait(false);
            await bans.LiftActiveAsync(b.GameLogin, "sistema", "Prazo do banimento terminou.", now, ct).ConfigureAwait(false);
            await audit.WriteAsync("system", "worker", "game.account.unbanned-auto", "game-account", b.GameLogin, null,
                System.Text.Json.JsonSerializer.Serialize(new { banId = b.Id, rows }), ct).ConfigureAwait(false);
        }

        return expired.Count;
    }

    public async Task<GameAdminResult> ChangeCoinsAsync(StaffPrincipal me, string login, CoinRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var errors = new Dictionary<string, string>();
        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        var mode = r.Mode is "add" or "remove" or "set" ? r.Mode : null;
        if (mode is null)
        {
            errors["mode"] = "Escolha adicionar, remover ou definir o saldo.";
        }

        var amount = r.Amount ?? -1;
        if (amount < (mode == "set" ? 0 : 1) || amount > int.MaxValue)
        {
            errors["amount"] = "Informe uma quantidade válida.";
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!IsValidLogin(login) || await game.GetAsync(login, ct).ConfigureAwait(false) is not { } snap)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        var limit = me.Role == StaffRoles.SuperAdmin ? long.MaxValue : O.MaxCoinChange;
        string? refusal = null;
        var change = await game.ChangeCoinsAsync(canonical, before =>
        {
            var after = mode switch { "add" => before + amount, "remove" => before - amount, _ => amount };
            if (after < 0)
            {
                refusal = "O saldo ficaria negativo.";
                return null;
            }

            if (after > int.MaxValue)
            {
                refusal = "O saldo passaria do limite do jogo.";
                return null;
            }

            if (Math.Abs(after - before) > limit)
            {
                refusal = $"Mudança acima de {limit.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))} coins: só o superadministrador pode fazer.";
                return null;
            }

            return after;
        }, ct).ConfigureAwait(false);

        switch (change.Outcome)
        {
            case CoinOutcome.Duplicate:
                return GameAdminResult.Conflict("A conta tem mais de uma linha de saldo em CoinShopDB.UserInfo. Corrija antes de mudar os coins (veja o diagnóstico).");
            case CoinOutcome.Rejected:
                return GameAdminResult.Invalid("amount", refusal ?? "Valor recusado.");
        }

        await GameAudit.WriteAsync(audit, me, "game.coins.changed", "game-account", canonical, client,
            new { mode, amount, before = change.Before, after = change.After, created = change.Created, reason }, ct).ConfigureAwait(false);
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        return GameAdminResult.Ok(new { change.Before, change.After },
            $"Saldo de coins: {change.Before.ToString("N0", pt)} → {change.After.ToString("N0", pt)}. Se o jogador estiver no jogo, o saldo novo aparece ao reabrir a loja.");
    }

    public async Task<GameAdminResult> AddVipAsync(StaffPrincipal me, string login, VipAddRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var errors = ValidateVip(me, r.Plan, r.Days, r.Reason, out var reason, allowNegative: false);
        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!IsValidLogin(login) || await game.GetAsync(login, ct).ConfigureAwait(false) is not { } snap)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        string? character = null;
        if (!string.IsNullOrWhiteSpace(r.Character))
        {
            character = snap.Characters.Select(c => c.Name.Trim()).FirstOrDefault(n => string.Equals(n, r.Character.Trim(), StringComparison.OrdinalIgnoreCase));
            if (character is null)
            {
                return GameAdminResult.Invalid("character", "Esse personagem não é desta conta.");
            }
        }

        var start = Trunc(GameTime.ToLocal(clock.GetUtcNow()));
        var end = start.AddDays(r.Days!.Value);
        await game.AddVipAsync(canonical, character, r.Plan!.Value, start, end, ct).ConfigureAwait(false);
        await GameAudit.WriteAsync(audit, me, "game.vip.added", "game-account", canonical, client,
            new { plan = r.Plan, character, days = r.Days, startLocal = start, endLocal = end, reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, $"VIP {PlanLabel(r.Plan)} adicionado até {end:dd/MM/yyyy HH:mm} (horário de Brasília).");
    }

    public async Task<GameAdminResult> ExtendVipAsync(StaffPrincipal me, string login, VipExtendRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        if (r.Row is null)
        {
            return GameAdminResult.Invalid("row", "Escolha o VIP.");
        }

        var errors = ValidateVip(me, r.Row.Plan ?? 0, r.Days, r.Reason, out var reason, allowNegative: true);
        errors.Remove("plan");
        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!IsValidLogin(login) || await game.GetAsync(login, ct).ConfigureAwait(false) is not { } snap)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        var original = new GameVipRow(r.Row.PlayerName, r.Row.Plan, r.Row.StartLocal, r.Row.EndLocal);
        var nowLocal = Trunc(GameTime.ToLocal(clock.GetUtcNow()));
        // VIP vencido recomeça a contar de agora; ativo soma ao fim atual.
        var baseEnd = original.EndLocal is { } e && e > nowLocal ? e : nowLocal;
        var newEnd = baseEnd.AddDays(r.Days!.Value);
        var start = original.StartLocal ?? nowLocal;
        if (newEnd <= start)
        {
            return GameAdminResult.Invalid("days", "O fim ficaria antes do início. Para acabar com o VIP, use Remover.");
        }

        var rows = await game.UpdateVipAsync(canonical, original, original.Plan ?? 0, start, newEnd, ct).ConfigureAwait(false);
        if (rows != 1)
        {
            return GameAdminResult.Conflict("Esse VIP mudou desde que a página abriu (ou existe repetido). Recarregue e tente de novo.");
        }

        await GameAudit.WriteAsync(audit, me, "game.vip.extended", "game-account", canonical, client,
            new { before = original, after = new { endLocal = newEnd }, days = r.Days, reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, $"VIP agora vai até {newEnd:dd/MM/yyyy HH:mm} (horário de Brasília).");
    }

    public async Task<GameAdminResult> RemoveVipAsync(StaffPrincipal me, string login, VipRemoveRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!game.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null || r.Row is null)
        {
            return GameAdminResult.Invalid("reason", reasonError ?? "Escolha o VIP.");
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        if (!IsValidLogin(login) || await game.GetAsync(login, ct).ConfigureAwait(false) is not { } snap)
        {
            return GameAdminResult.NotFound("Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        var original = new GameVipRow(r.Row.PlayerName, r.Row.Plan, r.Row.StartLocal, r.Row.EndLocal);
        var rows = await game.DeleteVipAsync(canonical, original, ct).ConfigureAwait(false);
        if (rows != 1)
        {
            return GameAdminResult.Conflict("Esse VIP mudou desde que a página abriu (ou existe repetido). Recarregue e tente de novo.");
        }

        await GameAudit.WriteAsync(audit, me, "game.vip.removed", "game-account", canonical, client, new { before = original, reason }, ct).ConfigureAwait(false);
        return GameAdminResult.Ok(null, "VIP removido.");
    }

    private Dictionary<string, string> ValidateVip(StaffPrincipal me, int? plan, int? days, string? reasonText, out string? reason, bool allowNegative)
    {
        var errors = new Dictionary<string, string>();
        reason = Reason.Clean(reasonText, out var reasonError);
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        if (plan is null or < 0 or > 1000)
        {
            errors["plan"] = "Escolha o plano de VIP.";
        }

        var max = me.Role == StaffRoles.SuperAdmin ? 3650 : O.MaxVipDays;
        if (days is null || days == 0 || days > max || days < (allowNegative ? -max : 1))
        {
            errors["days"] = allowNegative
                ? $"Use de 1 a {max} dias (negativo diminui)."
                : $"Use de 1 a {max} dias.";
        }

        return errors;
    }

    private string PlanLabel(int? plan) =>
        plan is null ? "Sem plano" : O.VipPlans.TryGetValue(plan.Value.ToString(CultureInfo.InvariantCulture), out var name) ? name : $"Plano {plan}";

    private static DateTime Trunc(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0, DateTimeKind.Unspecified);

    private static string Fmt(DateTimeOffset utc) => GameTime.ToLocal(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    internal static bool IsSet(string? flag) => !string.IsNullOrWhiteSpace(flag) && flag.Trim() != "0";
}
