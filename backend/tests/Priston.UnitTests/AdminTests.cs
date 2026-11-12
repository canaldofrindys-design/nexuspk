using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Content;

namespace Priston.UnitTests;

internal sealed class MemoryStaff : IStaffRepository
{
    public List<StaffUser> Rows { get; } = [];

    public Task<StaffUser?> FindByUsernameAsync(string username, CancellationToken ct) =>
        Task.FromResult(Rows.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<StaffUser?> FindByIdAsync(int id, CancellationToken ct) => Task.FromResult(Rows.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<StaffUser>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<StaffUser>>(Rows.ToList());

    public Task<int?> CreateAsync(string username, string displayName, string passwordHash, string role, string? createdBy, CancellationToken ct)
    {
        if (Rows.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult<int?>(null);
        }

        var id = Rows.Count + 1;
        Rows.Add(new StaffUser(id, username, displayName, passwordHash, role, true, true, 0, null, DateTimeOffset.UnixEpoch, null));
        return Task.FromResult<int?>(id);
    }

    private void Replace(int id, Func<StaffUser, StaffUser> f)
    {
        var i = Rows.FindIndex(u => u.Id == id);
        Rows[i] = f(Rows[i]);
    }

    public Task UpdateAsync(int id, string displayName, string role, bool active, CancellationToken ct)
    {
        Replace(id, u => u with { DisplayName = displayName, Role = role, Active = active });
        return Task.CompletedTask;
    }

    public Task SetPasswordAsync(int id, string passwordHash, bool mustChange, DateTimeOffset at, CancellationToken ct)
    {
        Replace(id, u => u with { PasswordHash = passwordHash, MustChangePassword = mustChange, FailedLogins = 0, LockedUntilUtc = null });
        return Task.CompletedTask;
    }

    public Task RecordLoginAsync(int id, DateTimeOffset at, CancellationToken ct)
    {
        Replace(id, u => u with { LastLoginUtc = at, FailedLogins = 0, LockedUntilUtc = null });
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(int id, DateTimeOffset? lockUntil, CancellationToken ct)
    {
        Replace(id, u => lockUntil is null ? u with { FailedLogins = u.FailedLogins + 1 } : u with { FailedLogins = 0, LockedUntilUtc = lockUntil });
        return Task.CompletedTask;
    }

    public Task<int> CountActiveSuperAdminsAsync(CancellationToken ct) => Task.FromResult(Rows.Count(u => u.Role == StaffRoles.SuperAdmin && u.Active));
}

internal sealed class MemoryStaffSessions : IStaffSessionStore
{
    public List<(byte[] Hash, int StaffId, DateTimeOffset Expires, bool Revoked)> Rows { get; } = [];

    public Task CreateAsync(byte[] tokenHash, int staffId, DateTimeOffset expiresUtc, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        Rows.Add((tokenHash, staffId, expiresUtc, false));
        return Task.CompletedTask;
    }

    public Task<(int StaffId, DateTimeOffset ExpiresUtc)?> FindActiveAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        var r = Rows.FirstOrDefault(s => s.Hash.SequenceEqual(tokenHash) && !s.Revoked && s.Expires > now);
        return Task.FromResult<(int, DateTimeOffset)?>(r.Hash is null ? null : (r.StaffId, r.Expires));
    }

    public Task RevokeAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Hash.SequenceEqual(tokenHash))
            {
                Rows[i] = Rows[i] with { Revoked = true };
            }
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllAsync(int staffId, byte[]? exceptTokenHash, DateTimeOffset now, CancellationToken ct)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].StaffId == staffId && (exceptTokenHash is null || !Rows[i].Hash.SequenceEqual(exceptTokenHash)))
            {
                Rows[i] = Rows[i] with { Revoked = true };
            }
        }

        return Task.CompletedTask;
    }
}

internal sealed class MemoryAudit : IAuditLog
{
    public List<(string Actor, string Action, string? Details)> Rows { get; } = [];

    public Task WriteAsync(string actorType, string? actorId, string action, string? targetType, string? targetId, string? ipAddress, string? details, CancellationToken ct)
    {
        Rows.Add((actorId ?? "", action, details));
        return Task.CompletedTask;
    }
}

internal sealed class MemoryContentAdmin : IContentAdminStore
{
    public Dictionary<int, NewsData> News { get; } = [];
    public Dictionary<int, EventData> Events { get; } = [];

    public Task<(IReadOnlyList<NewsRecord> Items, int Total)> ListNewsAsync(string? status, int skip, int take, CancellationToken ct) => throw new NotSupportedException();

    public Task<NewsRecord?> GetNewsAsync(int id, CancellationToken ct) => Task.FromResult(News.TryGetValue(id, out var d)
        ? new NewsRecord(id, d.Slug, d.Category, d.Title, d.Summary, d.Body, d.ImageUrl, d.Pinned, d.Status, d.PublishedUtc, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null)
        : null);

    public Task<int?> CreateNewsAsync(NewsData data, string by, CancellationToken ct)
    {
        if (News.Values.Any(n => n.Slug == data.Slug))
        {
            return Task.FromResult<int?>(null);
        }

        var id = News.Count + 1;
        News[id] = data;
        return Task.FromResult<int?>(id);
    }

    public Task<bool> UpdateNewsAsync(int id, NewsData data, string by, CancellationToken ct)
    {
        if (News.Any(n => n.Key != id && n.Value.Slug == data.Slug))
        {
            return Task.FromResult(false);
        }

        News[id] = data;
        return Task.FromResult(true);
    }

    public Task<(IReadOnlyList<EventRecord> Items, int Total)> ListEventsAsync(string? status, int skip, int take, CancellationToken ct) => throw new NotSupportedException();

    public Task<EventRecord?> GetEventAsync(int id, CancellationToken ct) => Task.FromResult(Events.TryGetValue(id, out var d)
        ? new EventRecord(id, d.Slug, d.Category, d.Name, d.Summary, d.Description, d.Rules, d.Prizes, d.Location, d.ImageUrl, d.StartsUtc, d.EndsUtc, d.Status,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null)
        : null);

    public Task<int?> CreateEventAsync(EventData data, string by, CancellationToken ct)
    {
        var id = Events.Count + 1;
        Events[id] = data;
        return Task.FromResult<int?>(id);
    }

    public Task<bool> UpdateEventAsync(int id, EventData data, string by, CancellationToken ct) => Task.FromResult(true);

    public Task<bool> DeleteNewsAsync(int id, CancellationToken ct) => Task.FromResult(News.Remove(id));

    public Task<bool> DeleteEventAsync(int id, CancellationToken ct) => Task.FromResult(Events.Remove(id));

    public Task EndEventAsync(int id, DateTimeOffset endsUtc, string by, CancellationToken ct)
    {
        Events[id] = Events[id] with { EndsUtc = endsUtc };
        return Task.CompletedTask;
    }
}

public sealed class AdminTests : IDisposable
{
    private static readonly DateTimeOffset Now = ContentFixture.Now;
    private static readonly ClientInfo Client = new("198.51.100.7", "teste");
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly MemoryStaff _staff = new();
    private readonly MemoryStaffSessions _sessions = new();
    private readonly MemoryAudit _audit = new();
    private readonly FixedClock _clock = new(Now);

    public void Dispose() => _cache.Dispose();

    private StaffAuthService Auth() => new(_staff, _sessions, _audit,
        new LoginThrottle(_cache, Options.Create(new AccountOptions()), _clock), _clock);

    private void Seed(string username, string password, string role, bool mustChange = false) =>
        _staff.Rows.Add(new StaffUser(_staff.Rows.Count + 1, username, username, StaffPassword.Hash(password), role, true, mustChange, 0, null, Now, null));

    private static StaffPrincipal As(int id, string username, string role) => new(id, username, username, role, false, Now.AddHours(1));

    [Fact]
    public void Password_hash_round_trips_and_rejects_tampering()
    {
        var hash = StaffPassword.Hash("cavalo-correto-bateria");
        Assert.StartsWith("pbkdf2-sha256$600000$", hash, StringComparison.Ordinal);
        Assert.True(StaffPassword.Verify("cavalo-correto-bateria", hash));
        Assert.False(StaffPassword.Verify("cavalo-correto-bateriA", hash));
        Assert.False(StaffPassword.Verify("x", "md5$1$abc$def"));
        Assert.NotNull(StaffPassword.Validate("curta", null));
        Assert.NotNull(StaffPassword.Validate("aaaaaaaaaaaaaaaa", null));
        Assert.NotNull(StaffPassword.Validate("joaozinho-senha-123", "joaozinho"));
        Assert.Null(StaffPassword.Validate("trocar-esta-senha-42", "ana"));
    }

    [Theory]
    [InlineData("superadmin", Capability.ManageStaff, true)]
    [InlineData("admin", Capability.ManageStaff, false)]
    [InlineData("admin", Capability.ViewAudit, true)]
    [InlineData("moderador", Capability.ManageContent, true)]
    [InlineData("moderador", Capability.ViewAudit, false)]
    [InlineData("suporte", Capability.ManageContent, false)]
    [InlineData("suporte", Capability.ViewDashboard, true)]
    public void Roles_grant_expected_capabilities(string role, Capability capability, bool expected) =>
        Assert.Equal(expected, StaffRoles.Can(role, capability));

    [Fact]
    public async Task Login_creates_session_and_five_failures_lock_the_account()
    {
        Seed("ana", "senha-forte-da-ana", StaffRoles.Admin);
        var auth = Auth();

        var ok = await auth.LoginAsync(new StaffLoginRequest("ana", "senha-forte-da-ana"), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);
        var me = await auth.GetCurrentAsync(ok.Token, CancellationToken.None);
        Assert.Equal("ana", me!.Username);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(StaffAuthStatus.InvalidCredentials, (await auth.LoginAsync(new StaffLoginRequest("ana", "errada-" + i), Client, CancellationToken.None)).Status);
        }

        Assert.Equal(StaffAuthStatus.Locked, (await auth.LoginAsync(new StaffLoginRequest("ana", "errada-5"), Client, CancellationToken.None)).Status);
        Assert.Contains(_audit.Rows, r => r.Action == "staff.locked");
        Assert.DoesNotContain(_audit.Rows, r => r.Details?.Contains("senha", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task Unknown_or_inactive_user_gets_same_answer()
    {
        Seed("bia", "senha-forte-da-bia", StaffRoles.Admin);
        _staff.Rows[0] = _staff.Rows[0] with { Active = false };
        var auth = Auth();
        Assert.Equal(StaffAuthStatus.InvalidCredentials, (await auth.LoginAsync(new StaffLoginRequest("bia", "senha-forte-da-bia"), Client, CancellationToken.None)).Status);
        Assert.Equal(StaffAuthStatus.InvalidCredentials, (await auth.LoginAsync(new StaffLoginRequest("ninguem", "qualquer-senha-1"), Client, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Changing_password_clears_flag_and_ends_other_sessions()
    {
        Seed("caio", "senha-temporaria-1", StaffRoles.Moderator, mustChange: true);
        var auth = Auth();
        var a = await auth.LoginAsync(new StaffLoginRequest("caio", "senha-temporaria-1"), Client, CancellationToken.None);
        var b = await auth.LoginAsync(new StaffLoginRequest("caio", "senha-temporaria-1"), Client, CancellationToken.None);
        Assert.True(a.MustChangePassword);
        var me = (await auth.GetCurrentAsync(a.Token, CancellationToken.None))!;

        var r = await auth.ChangePasswordAsync(me, a.Token!, new StaffChangePasswordRequest("senha-temporaria-1", "nova-senha-forte-9"), Client, CancellationToken.None);

        Assert.Equal(StaffAuthStatus.Ok, r.Status);
        Assert.False((await auth.GetCurrentAsync(a.Token, CancellationToken.None))!.MustChangePassword);
        Assert.Null(await auth.GetCurrentAsync(b.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Last_superadmin_cannot_be_demoted_and_nobody_edits_themselves()
    {
        Seed("root", "senha-do-superadmin", StaffRoles.SuperAdmin);
        Seed("outro", "senha-do-outro-adm", StaffRoles.Admin);
        var svc = new StaffAdminService(_staff, _sessions, _audit, _clock);
        var root = As(1, "root", StaffRoles.SuperAdmin);

        var self = await svc.UpdateAsync(root, 1, new UpdateStaffRequest(null, StaffRoles.Admin, true), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.ValidationFailed, self.Status);

        Assert.Equal(StaffAuthStatus.Ok, (await svc.UpdateAsync(root, 2, new UpdateStaffRequest(null, StaffRoles.SuperAdmin, true), Client, CancellationToken.None)).Status);
        var other = As(2, "outro", StaffRoles.SuperAdmin);
        Assert.Equal(StaffAuthStatus.Ok, (await svc.UpdateAsync(other, 1, new UpdateStaffRequest(null, StaffRoles.Admin, true), Client, CancellationToken.None)).Status);
        var last = await svc.UpdateAsync(As(1, "root", StaffRoles.Admin), 2, new UpdateStaffRequest(null, null, false), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.ValidationFailed, last.Status);
        Assert.Contains(_audit.Rows, r => r.Action == "staff.updated");
    }

    [Fact]
    public async Task Create_staff_validates_and_forces_password_change()
    {
        var svc = new StaffAdminService(_staff, _sessions, _audit, _clock);
        var root = As(1, "root", StaffRoles.SuperAdmin);
        var bad = await svc.CreateAsync(root, new CreateStaffRequest("x", "", "chefe", "curta"), Client, CancellationToken.None);
        Assert.Equal(["displayName", "role", "temporaryPassword", "username"], bad.Errors!.Keys.Order());

        var ok = await svc.CreateAsync(root, new CreateStaffRequest("dani", "Dani", StaffRoles.Support, "temporaria-segura-77"), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);
        Assert.True(_staff.Rows.Single().MustChangePassword);
        Assert.DoesNotContain(_audit.Rows, r => r.Details?.Contains("temporaria-segura-77", StringComparison.Ordinal) == true);
        var dup = await svc.CreateAsync(root, new CreateStaffRequest("DANI", "Dani 2", StaffRoles.Support, "temporaria-segura-77"), Client, CancellationToken.None);
        Assert.Equal("username", Assert.Single(dup.Errors!).Key);
    }

    [Fact]
    public async Task Content_admin_converts_brasilia_time_and_rejects_unsafe_images()
    {
        using var f = new ContentFixture();
        var store = new MemoryContentAdmin();
        var svc = new ContentAdminService(store, f.Service(), _audit, _clock);
        var mod = As(3, "mod", StaffRoles.Moderator);

        var bad = await svc.SaveEventAsync(mod, null, new EventInput("Com Espaço", "xyz", "", "r", "d", null, null, null, "javascript:alert(1)", "2026-13-01T10:00", "2026-11-01T09:00", "published"), Client, CancellationToken.None);
        Assert.Equal(["category", "imageUrl", "name", "slug", "startsLocal"], bad.Errors!.Keys.Order());

        var ok = await svc.SaveEventAsync(mod, null, new EventInput("boss-de-sabado", "boss", "Boss de sábado", "r", "d", null, null, "Ricarten", "/imagens/boss.png", "2026-11-14T20:00", "2026-11-14T22:00", "published"), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);
        var saved = store.Events[ok.Id!.Value];
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 23, 0, 0, TimeSpan.Zero), saved.StartsUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.Zero), saved.EndsUtc);

        var endsBefore = await svc.SaveEventAsync(mod, null, new EventInput("outro", "pvp", "PvP", "r", "d", null, null, null, null, "2026-11-14T20:00", "2026-11-14T19:00", "draft"), Client, CancellationToken.None);
        Assert.Equal("endsLocal", Assert.Single(endsBefore.Errors!).Key);
    }

    [Fact]
    public async Task Publishing_news_without_date_uses_now_and_duplicate_slug_is_reported()
    {
        using var f = new ContentFixture();
        var store = new MemoryContentAdmin();
        var svc = new ContentAdminService(store, f.Service(), _audit, _clock);
        var mod = As(3, "mod", StaffRoles.Moderator);
        var input = new NewsInput("abertura", "noticia", "Abertura", "Resumo", "Corpo", "https://cdn.example.test/a.png", false, "published", null);

        var first = await svc.SaveNewsAsync(mod, null, input, Client, CancellationToken.None);
        Assert.Equal(Now, store.News[first.Id!.Value].PublishedUtc);
        var dup = await svc.SaveNewsAsync(mod, null, input, Client, CancellationToken.None);
        Assert.Equal("slug", Assert.Single(dup.Errors!).Key);
        Assert.Contains(_audit.Rows, r => r.Action == "news.created" && r.Actor == "mod");
    }

    [Fact]
    public async Task Ending_an_event_sets_the_end_to_now_only_while_it_is_running()
    {
        using var f = new ContentFixture();
        var store = new MemoryContentAdmin();
        var svc = new ContentAdminService(store, f.Service(), _audit, _clock);
        var mod = As(3, "mod", StaffRoles.Moderator);
        store.Events[1] = new EventData("agora", "exp", "EXP", "r", "d", null, null, null, null, Now.AddMinutes(-30), Now.AddHours(2), "published");
        store.Events[2] = new EventData("depois", "exp", "EXP", "r", "d", null, null, null, null, Now.AddHours(1), null, "published");
        store.Events[3] = new EventData("rascunho", "exp", "EXP", "r", "d", null, null, null, null, Now.AddMinutes(-30), null, "draft");

        Assert.Equal(StaffAuthStatus.Ok, (await svc.EndEventAsync(mod, 1, Client, CancellationToken.None)).Status);
        Assert.Equal(Now, store.Events[1].EndsUtc);
        Assert.Contains(_audit.Rows, r => r.Action == "event.ended");
        Assert.Equal("end", Assert.Single((await svc.EndEventAsync(mod, 1, Client, CancellationToken.None)).Errors!).Key);
        Assert.Equal("end", Assert.Single((await svc.EndEventAsync(mod, 2, Client, CancellationToken.None)).Errors!).Key);
        Assert.Equal("end", Assert.Single((await svc.EndEventAsync(mod, 3, Client, CancellationToken.None)).Errors!).Key);
        Assert.Equal(StaffAuthStatus.NotFound, (await svc.EndEventAsync(mod, 99, Client, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Deleting_news_and_events_is_audited()
    {
        using var f = new ContentFixture();
        var store = new MemoryContentAdmin();
        var svc = new ContentAdminService(store, f.Service(), _audit, _clock);
        var mod = As(3, "mod", StaffRoles.Moderator);
        store.News[1] = new NewsData("velha", "noticia", "Velha", "r", "b", null, false, "archived", null);
        store.Events[1] = new EventData("antigo", "exp", "EXP", "r", "d", null, null, null, null, Now.AddDays(-3), null, "published");

        Assert.Equal(StaffAuthStatus.Ok, (await svc.DeleteNewsAsync(mod, 1, Client, CancellationToken.None)).Status);
        Assert.Equal(StaffAuthStatus.Ok, (await svc.DeleteEventAsync(mod, 1, Client, CancellationToken.None)).Status);
        Assert.Empty(store.News);
        Assert.Empty(store.Events);
        Assert.Equal(StaffAuthStatus.NotFound, (await svc.DeleteNewsAsync(mod, 1, Client, CancellationToken.None)).Status);
        Assert.Contains(_audit.Rows, r => r.Action == "news.deleted" && r.Details!.Contains("Velha", StringComparison.Ordinal));
        Assert.Contains(_audit.Rows, r => r.Action == "event.deleted");
    }

    [Fact]
    public void Only_admins_manage_site_texts()
    {
        Assert.True(StaffRoles.Can(StaffRoles.SuperAdmin, Capability.ManageSite));
        Assert.True(StaffRoles.Can(StaffRoles.Admin, Capability.ManageSite));
        Assert.False(StaffRoles.Can(StaffRoles.Moderator, Capability.ManageSite));
        Assert.False(StaffRoles.Can(StaffRoles.Support, Capability.ManageSite));
    }
}
