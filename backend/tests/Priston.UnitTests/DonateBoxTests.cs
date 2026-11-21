using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.GameAdmin;

namespace Priston.UnitTests;

public sealed class DonateBoxTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 11, 0, 0, TimeSpan.Zero);
    private static readonly ClientInfo Client = new("10.0.0.1", "teste");
    private static readonly StaffPrincipal Admin = new(1, "gm.alex", "Alex", StaffRoles.Admin, false, Now.AddHours(1));

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class MemoryDonate : IDonateBoxStore
    {
        public bool Enabled { get; set; } = true;
        public List<DonateBoxPackage> Created { get; } = [];
        public DonateRuntime? Runtime { get; set; }

        public Task<int> CreateAsync(DonateBoxPackage package, CancellationToken ct)
        {
            Created.Add(package);
            return Task.FromResult(1000 + Created.Count);
        }

        public Task<IReadOnlyList<DonateBoxSent>> ListRecentAsync(int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<DonateBoxSent>>([]);

        public Task<DonateRuntime?> RuntimeAsync(CancellationToken ct) => Task.FromResult(Runtime);
    }

    private static (DonateBoxService Svc, MemoryDonate Store, MemoryAudit Audit) Service()
    {
        var accounts = new FakeGameAccounts();
        accounts.Accounts["jogador1"] = FakeGameAccounts.Snapshot("jogador1", new GameCharacter("Guerreira ", 11, 120), new GameCharacter("SemClasse", 0, 10));
        var store = new MemoryDonate();
        var audit = new MemoryAudit();
        return (new DonateBoxService(store, accounts, audit, new FixedTime()), store, audit);
    }

    [Fact]
    public async Task Creates_the_package_with_the_class_of_the_character_and_one_line_per_unit()
    {
        var (svc, store, audit) = Service();

        var r = await svc.SendAsync(Admin, new DonateBoxRequest("JOGADOR1", "guerreira",
            [new("ca105", 2, 10, true), new("PL106", null, null, null)], "doação PIX 21/11"), Client, default);

        Assert.Equal(GameAdminStatus.Ok, r.Status);
        var p = Assert.Single(store.Created);
        Assert.Equal("jogador1", p.Login);
        Assert.Equal("Guerreira", p.Character);
        Assert.Equal(11, p.Spec);
        Assert.StartsWith("PORTAL-", p.RequestKey, StringComparison.Ordinal);
        Assert.Equal("Portal (gm.alex)", p.CreatedBy);
        Assert.Equal(["CA105", "CA105", "PL106"], p.Lines.Select(l => l.Code));
        Assert.Equal(10, p.Lines[0].Aging);
        Assert.True(p.Lines[0].Mix);
        Assert.False(p.Lines[2].Mix);
        Assert.Contains("\"package\":1001", audit.Rows.Single(a => a.Action == "game.donate.sent").Details!);
    }

    [Fact]
    public async Task Refuses_bad_items_and_sends_nothing()
    {
        var (svc, store, _) = Service();

        var r = await svc.SendAsync(Admin, new DonateBoxRequest("jogador1", "Guerreira",
            [new("BI354", 1, 0, false), new("ca-1", 1, 0, false), new("CA105", 0, 31, false)], "doação"), Client, default);

        Assert.Equal(GameAdminStatus.ValidationFailed, r.Status);
        Assert.Contains("items.0.code", r.Errors!.Keys);
        Assert.Contains("items.1.code", r.Errors!.Keys);
        Assert.Contains("items.2.quantity", r.Errors!.Keys);
        Assert.Contains("items.2.aging", r.Errors!.Keys);
        Assert.Empty(store.Created);
    }

    [Fact]
    public async Task A_box_holds_at_most_64_items()
    {
        var (svc, store, _) = Service();

        var r = await svc.SendAsync(Admin, new DonateBoxRequest("jogador1", "Guerreira", [new("CA105", 40, 0, false), new("PL106", 25, 0, false)], "evento"), Client, default);

        Assert.Equal(GameAdminStatus.ValidationFailed, r.Status);
        Assert.Contains("items", r.Errors!.Keys);
        Assert.Empty(store.Created);
    }

    [Fact]
    public async Task Character_must_belong_to_the_account_and_have_a_game_class()
    {
        var (svc, store, _) = Service();

        var other = await svc.SendAsync(Admin, new DonateBoxRequest("jogador1", "Fulano", [new("CA105", 1, 0, false)], "doação"), Client, default);
        var noClass = await svc.SendAsync(Admin, new DonateBoxRequest("jogador1", "SemClasse", [new("CA105", 1, 0, false)], "doação"), Client, default);
        var noAccount = await svc.SendAsync(Admin, new DonateBoxRequest("ninguem", "Guerreira", [new("CA105", 1, 0, false)], "doação"), Client, default);
        var noReason = await svc.SendAsync(Admin, new DonateBoxRequest("jogador1", "Guerreira", [new("CA105", 1, 0, false)], ""), Client, default);

        Assert.Contains("character", other.Errors!.Keys);
        Assert.Contains("character", noClass.Errors!.Keys);
        Assert.Contains("login", noAccount.Errors!.Keys);
        Assert.Contains("reason", noReason.Errors!.Keys);
        Assert.Empty(store.Created);
    }

    [Fact]
    public void The_distributor_is_online_while_it_keeps_signalling()
    {
        var (svc, _, _) = Service();

        // 11:00 UTC = 08:00 em Brasília.
        Assert.True(svc.IsOnline(new DonateRuntime(true, new DateTime(2026, 11, 21, 7, 55, 0), 1)));
        Assert.False(svc.IsOnline(new DonateRuntime(true, new DateTime(2026, 11, 21, 7, 40, 0), 1)));
        Assert.False(svc.IsOnline(new DonateRuntime(false, new DateTime(2026, 11, 21, 7, 59, 0), 1)));
        Assert.False(svc.IsOnline(null));
    }
}
