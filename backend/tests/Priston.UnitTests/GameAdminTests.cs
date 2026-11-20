using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.GameAdmin;

namespace Priston.UnitTests;

internal sealed class FakeGameAccounts : IGameAdminAccountStore
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, GameAccountSnapshot> Accounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public long Coins { get; set; }
    public bool CoinRowExists { get; set; } = true;
    public bool CoinDuplicate { get; set; }
    public List<(string Login, bool Blocked)> BlockCalls { get; } = [];
    public int VipChangeResult { get; set; } = 1;
    public List<(string? Player, int Plan, DateTime Start, DateTime End)> VipAdds { get; } = [];

    public Task<IReadOnlyList<AccountSearchHit>> SearchAsync(string term, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AccountSearchHit>>(Accounts.Keys.Where(k => k.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .Select(k => new AccountSearchHit(k, null, false, false, null)).ToList());

    public Task<GameAccountSnapshot?> GetAsync(string login, CancellationToken ct) => Task.FromResult(Accounts.GetValueOrDefault(login));

    public Task<int> SetBlockedAsync(string login, bool blocked, CancellationToken ct)
    {
        BlockCalls.Add((login, blocked));
        return Task.FromResult(1);
    }

    public Task<CoinChange> ChangeCoinsAsync(string login, Func<long, long?> compute, CancellationToken ct)
    {
        if (CoinDuplicate)
        {
            return Task.FromResult(new CoinChange(CoinOutcome.Duplicate, 0, 0, false));
        }

        var before = CoinRowExists ? Coins : 0;
        var after = compute(before);
        if (after is null)
        {
            return Task.FromResult(new CoinChange(CoinOutcome.Rejected, before, before, false));
        }

        var created = !CoinRowExists;
        Coins = after.Value;
        CoinRowExists = true;
        return Task.FromResult(new CoinChange(CoinOutcome.Ok, before, after.Value, created));
    }

    public Task AddVipAsync(string login, string? playerName, int plan, DateTime startLocal, DateTime endLocal, CancellationToken ct)
    {
        VipAdds.Add((playerName, plan, startLocal, endLocal));
        return Task.CompletedTask;
    }

    public Task<int> UpdateVipAsync(string login, GameVipRow original, int plan, DateTime startLocal, DateTime endLocal, CancellationToken ct) => Task.FromResult(VipChangeResult);

    public Task<int> DeleteVipAsync(string login, GameVipRow original, CancellationToken ct) => Task.FromResult(VipChangeResult);

    public static GameAccountSnapshot Snapshot(string login, params GameCharacter[] chars) => new(
        [new GameAccountRow(login, new DateTime(2026, 1, 1), null, "0", "0", "U")], chars, [], [100], null, [], [], []);
}

internal sealed class MemoryBans : IGameBanStore
{
    public List<GameBanRecord> Rows { get; } = [];

    public Task<long> CreateAsync(string login, string reason, string bannedBy, DateTimeOffset now, DateTimeOffset? expiresUtc, CancellationToken ct)
    {
        var id = Rows.Count + 1;
        Rows.Add(new GameBanRecord(id, login, reason, bannedBy, now, expiresUtc, null, null, null));
        return Task.FromResult((long)id);
    }

    public Task<IReadOnlyList<GameBanRecord>> ListAsync(string login, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GameBanRecord>>(Rows.Where(r => r.GameLogin == login).ToList());

    public Task LiftActiveAsync(string login, string liftedBy, string reason, DateTimeOffset now, CancellationToken ct)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].GameLogin == login && Rows[i].LiftedUtc is null)
            {
                Rows[i] = Rows[i] with { LiftedUtc = now, LiftedBy = liftedBy, LiftReason = reason };
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameBanRecord>> ListExpiredActiveAsync(DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GameBanRecord>>(Rows.Where(r => r.LiftedUtc is null && r.ExpiresUtc <= now).ToList());
}

internal sealed class NoActivity : IAccountActivityReader
{
    public Task<IReadOnlyList<AuditEntry>> ListForAccountAsync(string login, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditEntry>>([]);

    public Task<int> CountActivePlayerSessionsAsync(string login, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
}

internal sealed class RevokingSessions : IPlayerSessionStore
{
    public List<string> RevokedLogins { get; } = [];

    public Task CreateAsync(byte[] tokenHash, string gameLogin, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct) => Task.CompletedTask;

    public Task<PlayerSession?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct) => Task.FromResult<PlayerSession?>(null);

    public Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;

    public Task RevokeAllAsync(string gameLogin, DateTimeOffset now, CancellationToken ct)
    {
        RevokedLogins.Add(gameLogin);
        return Task.CompletedTask;
    }

    public Task<int> RevokeAllExceptAsync(string gameLogin, byte[] keepTokenHash, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
}

internal sealed class FakeRankingStore : IRankingResetStore
{
    public bool Enabled => true;
    public List<string> Calls { get; } = [];

    public Task<IReadOnlyList<RankingBackupRow>> ReadAsync(RankingResetKind kind, CancellationToken ct)
    {
        Calls.Add("read");
        return Task.FromResult<IReadOnlyList<RankingBackupRow>>([new(new() { ["Account"] = "a", ["Nick"] = "N" }, new() { ["BPsSemanal"] = 50 })]);
    }

    public Task<int> ResetAsync(RankingResetKind kind, CancellationToken ct)
    {
        Calls.Add("reset");
        return Task.FromResult(1);
    }

    public Task<int> RestoreAsync(RankingResetKind kind, IReadOnlyList<RankingBackupRow> rows, CancellationToken ct)
    {
        Calls.Add($"restore:{rows.Count}:{rows[0].Values["BPsSemanal"]}");
        return Task.FromResult(rows.Count);
    }
}

internal sealed class MemoryBackups : IRankingBackupStore
{
    public Dictionary<long, (RankingBackup Info, string Json)> Rows { get; } = [];

    public Task<long> CreateAsync(string kind, string createdBy, DateTimeOffset now, int rows, string json, CancellationToken ct)
    {
        var id = Rows.Count + 1L;
        Rows[id] = (new RankingBackup(id, kind, createdBy, now, rows, null, null), json);
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<RankingBackup>> ListAsync(int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<RankingBackup>>(Rows.Values.Select(v => v.Info).ToList());

    public Task<(RankingBackup Info, string Json)?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult<(RankingBackup, string)?>(Rows.TryGetValue(id, out var v) ? v : null);

    public Task MarkRestoredAsync(long id, string by, DateTimeOffset now, CancellationToken ct)
    {
        Rows[id] = (Rows[id].Info with { RestoredUtc = now, RestoredBy = by }, Rows[id].Json);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSchema : IGameSchemaReader
{
    public bool Enabled => true;
    public Dictionary<string, IReadOnlyList<GameColumn>> Tables { get; } = [];

    public Task<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(["ServerDB", "KLVN Studio"]);

    public Task<IReadOnlyList<GameTableInfo>> ListTablesAsync(string database, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GameTableInfo>>(Tables.Keys.Select(k => new GameTableInfo(k, 10)).ToList());

    public Task<IReadOnlyList<GameColumn>> GetColumnsAsync(string database, string table, CancellationToken ct) =>
        Task.FromResult(Tables.GetValueOrDefault(table) ?? []);
}

internal sealed class MemoryTables : IGameTableStore
{
    public List<Dictionary<string, object?>> Rows { get; } = [];
    public List<string> Calls { get; } = [];

    public Task<GameRowPage> QueryAsync(ResolvedTable table, IReadOnlyList<GameTableFilter> filters, int skip, int take, CancellationToken ct) =>
        Task.FromResult(new GameRowPage(Rows.Select(r => (IReadOnlyDictionary<string, object?>)r).ToList(), false));

    public Task<RowWrite> UpdateAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    {
        var match = Rows.Where(r => keys.All(k => Equals(r[k.Key], k.Value))).ToList();
        if (match.Count != 1)
        {
            return Task.FromResult(new RowWrite(match.Count == 0 ? RowWriteOutcome.NotFound : RowWriteOutcome.Ambiguous, null));
        }

        var before = new Dictionary<string, object?>(match[0]);
        foreach (var (k, v) in values)
        {
            match[0][k] = v;
        }

        return Task.FromResult(new RowWrite(RowWriteOutcome.Ok, before));
    }

    public Task<bool> InsertAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> values, IReadOnlyDictionary<string, object?>? uniqueKeys, CancellationToken ct)
    {
        if (uniqueKeys is not null && Rows.Any(r => uniqueKeys.All(k => Equals(r.GetValueOrDefault(k.Key), k.Value))))
        {
            return Task.FromResult(false);
        }

        Rows.Add(new Dictionary<string, object?>(values));
        return Task.FromResult(true);
    }

    public Task<RowWrite> DeleteAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> keys, CancellationToken ct)
    {
        var match = Rows.Where(r => keys.All(k => Equals(r[k.Key], k.Value))).ToList();
        if (match.Count == 1)
        {
            Rows.Remove(match[0]);
        }

        return Task.FromResult(new RowWrite(match.Count == 1 ? RowWriteOutcome.Ok : RowWriteOutcome.NotFound, match.FirstOrDefault()));
    }
}

internal sealed class MemoryBindings : IGameSourceBindingStore
{
    public Dictionary<string, GameSourceBinding> Rows { get; } = [];

    public Task<IReadOnlyList<GameSourceBinding>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<GameSourceBinding>>(Rows.Values.ToList());

    public Task<GameSourceBinding?> GetAsync(string functionKey, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault(functionKey));

    public Task SaveAsync(GameSourceBinding binding, CancellationToken ct)
    {
        Rows[binding.FunctionKey] = binding;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string functionKey, CancellationToken ct)
    {
        Rows.Remove(functionKey);
        return Task.CompletedTask;
    }
}

internal sealed class KnownGameAccounts(params string[] logins) : IGameAccountStore
{
    public bool WritesEnabled => false;

    public Task<bool> ExistsAsync(string login, CancellationToken ct) => Task.FromResult(logins.Contains(login, StringComparer.OrdinalIgnoreCase));

    public Task<GameCredentialCheck> CheckPasswordAsync(string login, string password, CancellationToken ct) => throw new NotSupportedException();

    public Task<GameAccountCreateResult> CreateAsync(string login, string password, string? ipAddress, CancellationToken ct) => throw new NotSupportedException();

    public Task<bool> ChangePasswordAsync(string login, string newPassword, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class FakeController : IServerProcessController
{
    public List<string> Calls { get; } = [];

    public ServerProcessStatus GetStatus(ServerProcessOptions process) => new(process.Key, process.Name, ProcessState.Running, 1, null, null);

    public Task<ProcessActionResult> StartAsync(ServerProcessOptions process, CancellationToken ct)
    {
        Calls.Add("start:" + process.Key);
        return Task.FromResult(new ProcessActionResult(true, "ligado."));
    }

    public Task<ProcessActionResult> StopAsync(ServerProcessOptions process, TimeSpan timeout, CancellationToken ct)
    {
        Calls.Add("stop:" + process.Key);
        return Task.FromResult(new ProcessActionResult(true, "desligado."));
    }
}

public sealed class GameAdminTests
{
    private const string Password = "senha-forte-do-admin-123";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    private static readonly ClientInfo Client = new("10.0.0.1", "teste");

    private readonly MemoryStaff staff = new();
    private readonly MemoryStaffSessions staffSessions = new();
    private readonly MemoryAudit audit = new();
    private readonly FakeClock clock = new(Now);

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private StaffPrincipal Member(string role)
    {
        var id = staff.Rows.Count + 1;
        staff.Rows.Add(new StaffUser(id, $"membro{id}", "Membro", StaffPassword.Hash(Password), role, true, false, 0, null, Now, null));
        return new StaffPrincipal(id, $"membro{id}", "Membro", role, false, Now.AddHours(1));
    }

    private StaffReauthService Reauth() => new(staff, staffSessions, audit, clock);

    private (GameAccountAdminService Svc, FakeGameAccounts Game, MemoryBans Bans, RevokingSessions Sessions) Accounts(GameAdminOptions? o = null)
    {
        var game = new FakeGameAccounts();
        var bans = new MemoryBans();
        var sessions = new RevokingSessions();
        var svc = new GameAccountAdminService(game, bans, new NoActivity(), new MemoryAccounts(), sessions, Reauth(), audit, Options.Create(o ?? new GameAdminOptions()), clock);
        return (svc, game, bans, sessions);
    }

    [Theory]
    [InlineData("superadmin", Capability.ControlServer, true)]
    [InlineData("admin", Capability.ControlServer, false)]
    [InlineData("admin", Capability.ResetRankings, false)]
    [InlineData("admin", Capability.ManageEconomy, true)]
    [InlineData("admin", Capability.ConfigureGameSources, false)]
    [InlineData("moderador", Capability.ManageAccounts, true)]
    [InlineData("moderador", Capability.ManageEconomy, false)]
    [InlineData("suporte", Capability.ViewAccounts, true)]
    [InlineData("suporte", Capability.ManageAccounts, false)]
    public void Roles_cover_the_new_panel(string role, Capability cap, bool expected) => Assert.Equal(expected, StaffRoles.Can(role, cap));

    [Fact]
    public async Task Ban_needs_the_staff_password_and_counts_failures()
    {
        var (svc, game, bans, _) = Accounts();
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        var me = Member(StaffRoles.Moderator);

        var r = await svc.BanAsync(me, "jogador1", new BanRequest("uso de hack", 7, "errada"), Client, default);

        Assert.Equal(GameAdminStatus.ReauthFailed, r.Status);
        Assert.Empty(game.BlockCalls);
        Assert.Empty(bans.Rows);
        Assert.Equal(1, staff.Rows[0].FailedLogins);
        Assert.Contains(audit.Rows, a => a.Action == "staff.reauth-failed");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_and_end_sessions()
    {
        var (svc, game, _, _) = Accounts();
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        var me = Member(StaffRoles.Admin);
        await staffSessions.CreateAsync([1], me.Id, Now.AddHours(1), null, null, default);
        for (var i = 0; i < 5; i++)
        {
            await svc.BanAsync(me, "jogador1", new BanRequest("motivo", null, "errada"), Client, default);
        }

        Assert.NotNull(staff.Rows[0].LockedUntilUtc);
        Assert.All(staffSessions.Rows, s => Assert.True(s.Revoked));
        // Bloqueado: nem a senha certa passa.
        var r = await svc.BanAsync(me, "jogador1", new BanRequest("motivo", null, Password), Client, default);
        Assert.Equal(GameAdminStatus.ReauthFailed, r.Status);
    }

    [Fact]
    public async Task Ban_blocks_records_history_and_ends_player_sessions()
    {
        var (svc, game, bans, sessions) = Accounts();
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        var me = Member(StaffRoles.Moderator);

        var r = await svc.BanAsync(me, "jogador1", new BanRequest("uso de hack", 7, Password), Client, default);

        Assert.Equal(GameAdminStatus.Ok, r.Status);
        Assert.Equal(("jogador1", true), game.BlockCalls.Single());
        Assert.Equal(Now.AddDays(7), bans.Rows.Single().ExpiresUtc);
        Assert.Equal("jogador1", sessions.RevokedLogins.Single());
        var entry = audit.Rows.Single(a => a.Action == "game.account.banned");
        Assert.Contains("uso de hack", entry.Details);
    }

    [Fact]
    public async Task Only_superadmin_bans_a_game_master()
    {
        var (svc, game, _, _) = Accounts();
        game.Accounts["gm1"] = FakeGameAccounts.Snapshot("gm1") with { GameMasterLevel = 3 };
        var r = await svc.BanAsync(Member(StaffRoles.Admin), "gm1", new BanRequest("teste de ban", null, Password), Client, default);
        Assert.Equal(GameAdminStatus.ValidationFailed, r.Status);
        Assert.Empty(game.BlockCalls);
    }

    [Fact]
    public async Task Expired_bans_are_lifted_by_the_worker()
    {
        var (svc, game, bans, _) = Accounts();
        await bans.CreateAsync("jogador1", "x", "membro", Now.AddDays(-8), Now.AddDays(-1), default);
        await bans.CreateAsync("jogador2", "y", "membro", Now.AddDays(-1), null, default);

        Assert.Equal(1, await svc.LiftExpiredAsync(default));
        Assert.Equal(("jogador1", false), game.BlockCalls.Single());
        Assert.NotNull(bans.Rows[0].LiftedUtc);
        Assert.Null(bans.Rows[1].LiftedUtc);
    }

    [Theory]
    [InlineData("add", 50, 150)]
    [InlineData("remove", 30, 70)]
    [InlineData("set", 0, 0)]
    public async Task Coins_change_and_audit_before_and_after(string mode, long amount, long expected)
    {
        var (svc, game, _, _) = Accounts();
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        game.Coins = 100;

        var r = await svc.ChangeCoinsAsync(Member(StaffRoles.Admin), "jogador1", new CoinRequest(mode, amount, "doação PIX"), Client, default);

        Assert.Equal(GameAdminStatus.Ok, r.Status);
        Assert.Equal(expected, game.Coins);
        var details = audit.Rows.Single(a => a.Action == "game.coins.changed").Details!;
        Assert.Contains("\"before\":100", details);
        Assert.Contains($"\"after\":{expected}", details);
    }

    [Fact]
    public async Task Coins_never_go_negative_and_respect_the_limit()
    {
        var (svc, game, _, _) = Accounts(new GameAdminOptions { MaxCoinChange = 1000 });
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        game.Coins = 10;
        var admin = Member(StaffRoles.Admin);

        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.ChangeCoinsAsync(admin, "jogador1", new CoinRequest("remove", 11, "estorno"), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.ChangeCoinsAsync(admin, "jogador1", new CoinRequest("add", 1001, "bônus"), Client, default)).Status);
        Assert.Equal(GameAdminStatus.Ok, (await svc.ChangeCoinsAsync(Member(StaffRoles.SuperAdmin), "jogador1", new CoinRequest("add", 5000, "bônus"), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.ChangeCoinsAsync(admin, "jogador1", new CoinRequest("add", 10, ""), Client, default)).Status);
        Assert.Equal(5010, game.Coins);
    }

    [Fact]
    public async Task Duplicate_coin_rows_are_not_touched()
    {
        var (svc, game, _, _) = Accounts();
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        game.CoinDuplicate = true;
        var r = await svc.ChangeCoinsAsync(Member(StaffRoles.Admin), "jogador1", new CoinRequest("add", 1, "teste"), Client, default);
        Assert.Equal(GameAdminStatus.Conflict, r.Status);
    }

    [Fact]
    public async Task Vip_only_for_characters_of_the_account_and_within_limit()
    {
        var (svc, game, _, _) = Accounts(new GameAdminOptions { MaxVipDays = 30 });
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1", new GameCharacter("Heroi", 1, 100));
        var admin = Member(StaffRoles.Admin);

        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.AddVipAsync(admin, "jogador1", new VipAddRequest(1, "OutroNome", 10, "compra"), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.AddVipAsync(admin, "jogador1", new VipAddRequest(1, null, 31, "compra"), Client, default)).Status);
        var ok = await svc.AddVipAsync(admin, "jogador1", new VipAddRequest(2, "heroi", 30, "compra"), Client, default);

        Assert.Equal(GameAdminStatus.Ok, ok.Status);
        var add = game.VipAdds.Single();
        Assert.Equal("Heroi", add.Player);
        Assert.Equal(30, (add.End - add.Start).TotalDays);
    }

    [Fact]
    public async Task Vip_change_on_a_row_that_moved_is_a_conflict()
    {
        var (svc, game, _, _) = Accounts();
        game.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1");
        game.VipChangeResult = 0;
        var row = new VipRowRef(null, 1, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1));
        var r = await svc.ExtendVipAsync(Member(StaffRoles.Admin), "jogador1", new VipExtendRequest(row, 10, "bônus"), Client, default);
        Assert.Equal(GameAdminStatus.Conflict, r.Status);
    }

    [Fact]
    public void Diagnosis_points_out_real_problems()
    {
        var (svc, _, _, _) = Accounts();
        var snap = new GameAccountSnapshot(
            [new GameAccountRow("dup", null, null, "1", "0", "U"), new GameAccountRow("dup", null, null, "0", "0", "U")],
            [new GameCharacter("Nome", 1, 10)],
            [new GameVipRow(null, 1, new DateTime(2026, 11, 1), new DateTime(2027, 1, 1)), new GameVipRow("Outro", 2, new DateTime(2026, 11, 10), new DateTime(2027, 1, 1))],
            [5, 7], null, [], [], ["Nome"]);

        var keys = svc.Diagnose(snap, null, 0, [], Now).ToDictionary(d => d.Key, d => d.Level);

        Assert.Equal("problema", keys["account-duplicate"]);
        Assert.Equal("aviso", keys["blocked"]);
        Assert.Equal("problema", keys["coins-duplicate"]);
        Assert.Equal("aviso", keys["vip-overlap"]);
        Assert.Equal("aviso", keys["vip-character"]);
        Assert.Equal("problema", keys["character-shared"]);
        Assert.Equal("info", keys["portal-none"]);
    }

    [Fact]
    public async Task Ranking_reset_backs_up_first_and_can_be_undone()
    {
        var store = new FakeRankingStore();
        var backups = new MemoryBackups();
        var svc = new RankingResetService(store, backups, Reauth(), audit, clock);
        var me = Member(StaffRoles.SuperAdmin);

        var wrongWord = await svc.ResetAsync(me, new RankingResetRequest("pvp-semanal", "zerar", "fim da temporada", Password), Client, default);
        Assert.Equal(GameAdminStatus.ValidationFailed, wrongWord.Status);
        Assert.Empty(store.Calls);

        var ok = await svc.ResetAsync(me, new RankingResetRequest("pvp-semanal", "ZERAR", "fim da temporada", Password), Client, default);
        Assert.Equal(GameAdminStatus.Ok, ok.Status);
        Assert.Equal(["read", "reset"], store.Calls);
        Assert.Single(backups.Rows);

        var undo = await svc.RestoreAsync(me, 1, new RankingRestoreRequest("reset por engano", Password), Client, default);
        Assert.Equal(GameAdminStatus.Ok, undo.Status);
        Assert.Equal("restore:1:50", store.Calls[^1]);
        Assert.Equal(GameAdminStatus.Conflict, (await svc.RestoreAsync(me, 1, new RankingRestoreRequest("de novo", Password), Client, default)).Status);
    }

    [Fact]
    public async Task Server_control_needs_the_typed_word_and_stops_in_reverse_order()
    {
        var ctl = new FakeController();
        var opts = new GameServerControlOptions
        {
            CooldownSeconds = 0,
            Processes =
            [
                new() { Key = "login", Name = "Login", ExecutablePath = "C:\\PT\\login.exe", StartDelaySeconds = 0 },
                new() { Key = "game1", Name = "Canal 1", ExecutablePath = "C:\\PT\\game.exe", StartDelaySeconds = 0 },
            ],
        };
        var svc = new ServerControlService(ctl, Options.Create(opts), Reauth(), audit, clock);
        var me = Member(StaffRoles.SuperAdmin);

        var bad = await svc.RunAsync(me, new ServerControlRequest("restart", "*", "LIGAR", "manutenção", Password), Client, default);
        Assert.Equal(GameAdminStatus.ValidationFailed, bad.Status);
        Assert.Empty(ctl.Calls);

        var ok = await svc.RunAsync(me, new ServerControlRequest("restart", "*", "REINICIAR", "manutenção", Password), Client, default);
        Assert.Equal(GameAdminStatus.Ok, ok.Status);
        Assert.Equal(["stop:game1", "stop:login", "start:login", "start:game1"], ctl.Calls);
        Assert.Contains(audit.Rows, a => a.Action == "game.server.restart");
    }

    private static readonly GameColumn Code = new("ItemCode", "varchar", 20, 0, 0, false, false, false, false);
    private static readonly GameColumn Price = new("Price", "int", 4, 10, 0, false, false, false, false);
    private static readonly GameColumn Note = new("Note", "nvarchar", 100, 0, 0, true, false, false, false);
    private static readonly GameColumn Id = new("Id", "int", 4, 10, 0, false, true, false, false);

    private (GameTableService Svc, FakeSchema Schema, MemoryTables Tables, MemoryBindings Bindings) Tables()
    {
        var schema = new FakeSchema();
        schema.Tables["SkinShop"] = [Id, Code, Price, Note];
        schema.Tables["Delivery"] = [Id, new GameColumn("UserID", "varchar", 32, 0, 0, false, false, false, false), Code, new GameColumn("Qty", "int", 4, 10, 0, false, false, false, true)];
        var tables = new MemoryTables();
        var bindings = new MemoryBindings();
        var svc = new GameTableService(schema, tables, bindings, new KnownGameAccounts("jogador1"), Reauth(), audit, new MemoryCache(new MemoryCacheOptions()), clock);
        return (svc, schema, tables, bindings);
    }

    private static SourceConfigRequest Config(string table, string[] keys, string[] editable, string? account = null) =>
        new("ServerDB", table, keys, editable, ["ItemCode"], account, true, true, false, Password);

    [Fact]
    public async Task Config_checks_columns_against_the_real_schema()
    {
        var (svc, _, _, bindings) = Tables();
        var me = Member(StaffRoles.SuperAdmin);

        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.SaveConfigAsync(me, "skin-shop", Config("SkinShop", [], ["Price"]), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.SaveConfigAsync(me, "skin-shop", Config("SkinShop", ["ItemCode"], ["Inventada"]), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.SaveConfigAsync(me, "skin-shop", Config("SkinShop", ["ItemCode"], ["Id"]), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.SaveConfigAsync(me, "skin-shop", Config("Nao_Existe", ["ItemCode"], ["Price"]), Client, default)).Status);
        Assert.Equal(GameAdminStatus.ValidationFailed, (await svc.SaveConfigAsync(me, "skin-shop", Config("Skin;DROP", ["ItemCode"], ["Price"]), Client, default)).Status);
        Assert.Empty(bindings.Rows);

        var ok = await svc.SaveConfigAsync(me, "skin-shop", Config("SkinShop", ["ItemCode"], ["Price", "Note"]), Client, default);
        Assert.Equal(GameAdminStatus.Ok, ok.Status);
        Assert.Equal(["ItemCode"], bindings.Rows["skin-shop"].KeyColumns);
    }

    [Fact]
    public async Task Editor_updates_one_row_with_typed_values_and_audits()
    {
        var (svc, _, tables, _) = Tables();
        var super = Member(StaffRoles.SuperAdmin);
        await svc.SaveConfigAsync(super, "skin-shop", Config("SkinShop", ["ItemCode"], ["Price"]), Client, default);
        tables.Rows.Add(new() { ["Id"] = 1, ["ItemCode"] = "SK01", ["Price"] = 100, ["Note"] = null });
        var admin = Member(StaffRoles.Admin);

        var bad = await svc.UpdateRowAsync(admin, "skin-shop", new RowUpdateRequest(new() { ["ItemCode"] = "SK01" }, new() { ["Price"] = "abc" }, "ajuste de preço"), Client, default);
        Assert.Equal(GameAdminStatus.ValidationFailed, bad.Status);

        // Coluna fora das editáveis é ignorada; Price vira int.
        var ok = await svc.UpdateRowAsync(admin, "skin-shop", new RowUpdateRequest(new() { ["ItemCode"] = "SK01" }, new() { ["Price"] = "250", ["Note"] = "hack" }, "ajuste de preço"), Client, default);
        Assert.Equal(GameAdminStatus.Ok, ok.Status);
        Assert.Equal(250, tables.Rows[0]["Price"]);
        Assert.Null(tables.Rows[0]["Note"]);
        var details = audit.Rows.Single(a => a.Action == "game.skin-shop.updated").Details!;
        Assert.Contains("\"Price\":\"100\"", details);
        Assert.Contains("\"Price\":\"250\"", details);

        var moderator = Member(StaffRoles.Moderator);
        var denied = await svc.UpdateRowAsync(moderator, "skin-shop", new RowUpdateRequest(new() { ["ItemCode"] = "SK01" }, new() { ["Price"] = "1" }, "teste"), Client, default);
        Assert.Equal(GameAdminStatus.Forbidden, denied.Status);
    }

    [Fact]
    public async Task Box_send_only_to_existing_accounts()
    {
        var (svc, _, tables, _) = Tables();
        var super = Member(StaffRoles.SuperAdmin);
        var cfg = new SourceConfigRequest("ServerDB", "Delivery", [], [], ["UserID"], "UserID", false, false, true, Password);
        Assert.Equal(GameAdminStatus.Ok, (await svc.SaveConfigAsync(super, "box-send", cfg, Client, default)).Status);
        var admin = Member(StaffRoles.Admin);

        var missing = await svc.InsertRowAsync(admin, "box-send", new RowInsertRequest(new() { ["UserID"] = "naoexiste", ["ItemCode"] = "BOX1" }, "doação"), Client, default);
        Assert.Equal(GameAdminStatus.ValidationFailed, missing.Status);
        Assert.Empty(tables.Rows);

        var ok = await svc.InsertRowAsync(admin, "box-send", new RowInsertRequest(new() { ["UserID"] = "jogador1", ["ItemCode"] = "BOX1", ["Id"] = "99" }, "doação"), Client, default);
        Assert.Equal(GameAdminStatus.Ok, ok.Status);
        var row = tables.Rows.Single();
        Assert.False(row.ContainsKey("Id"));   // identidade nunca é gravada pelo painel
        Assert.False(row.ContainsKey("Qty"));  // vazio com padrão: fica o padrão da tabela
        Assert.Equal("jogador1", row["UserID"]);
    }

    [Fact]
    public async Task Delete_needs_password()
    {
        var (svc, _, tables, _) = Tables();
        var super = Member(StaffRoles.SuperAdmin);
        await svc.SaveConfigAsync(super, "npc-shops", Config("SkinShop", ["ItemCode"], ["Price"]), Client, default);
        tables.Rows.Add(new() { ["Id"] = 1, ["ItemCode"] = "N1", ["Price"] = 5, ["Note"] = null });

        Assert.Equal(GameAdminStatus.ReauthFailed, (await svc.DeleteRowAsync(super, "npc-shops", new RowDeleteRequest(new() { ["ItemCode"] = "N1" }, "item errado", "x"), Client, default)).Status);
        Assert.Single(tables.Rows);
        Assert.Equal(GameAdminStatus.Ok, (await svc.DeleteRowAsync(super, "npc-shops", new RowDeleteRequest(new() { ["ItemCode"] = "N1" }, "item errado", Password), Client, default)).Status);
        Assert.Empty(tables.Rows);
    }

    [Fact]
    public async Task Logs_are_read_only()
    {
        var (svc, schema, _, bindings) = Tables();
        schema.Tables["ItemLog"] = [Id, Code];
        var super = Member(StaffRoles.SuperAdmin);
        Assert.Equal(GameAdminStatus.Ok, (await svc.SaveConfigAsync(super, "item-logs", new SourceConfigRequest("ServerDB", "ItemLog", ["Id"], ["ItemCode"], ["ItemCode"], null, true, true, true, Password), Client, default)).Status);
        Assert.Empty(bindings.Rows["item-logs"].EditableColumns);
        Assert.False(bindings.Rows["item-logs"].AllowDelete);
        var r = await svc.InsertRowAsync(super, "item-logs", new RowInsertRequest(new() { ["ItemCode"] = "x" }, "teste"), Client, default);
        Assert.Equal(GameAdminStatus.ValidationFailed, r.Status);
    }

    [Theory]
    [InlineData("int", "12", true)]
    [InlineData("int", "1.5", false)]
    [InlineData("tinyint", "300", false)]
    [InlineData("varchar", "çã", true)]
    [InlineData("varchar", "日本", false)]
    [InlineData("datetime", "2026-11-20T14:30", true)]
    [InlineData("datetime", "20/11/2026", false)]
    [InlineData("bit", "1", true)]
    [InlineData("decimal", "10,50", true)]
    public void Values_are_parsed_by_the_column_type(string type, string input, bool ok)
    {
        var col = new GameColumn("C", type, type == "varchar" ? 10 : 4, 10, 2, false, false, false, false);
        Assert.Equal(ok, GameValues.TryParse(col, input, out _, out _));
    }

    [Fact]
    public void Dates_round_trip_with_milliseconds()
    {
        var d = new DateTime(2026, 11, 20, 14, 30, 5, 123);
        var col = new GameColumn("D", "datetime", 8, 23, 3, false, false, false, false);
        Assert.True(GameValues.TryParse(col, GameValues.Format(d), out var back, out _));
        Assert.Equal(d, back);
    }
}
