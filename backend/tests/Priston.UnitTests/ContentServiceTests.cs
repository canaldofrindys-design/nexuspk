using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Content;
using Priston.Application.Home;

namespace Priston.UnitTests;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class MemoryNews : INewsReader
{
    public List<(NewsArticle Article, bool Pinned)> Rows { get; } = [];

    public Task<(IReadOnlyList<NewsSummary> Items, int Total)> ListPublishedAsync(string? category, int skip, int take, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var visible = Rows.Where(r => r.Article.PublishedUtc <= nowUtc && (category is null || r.Article.Category == category))
            .OrderByDescending(r => r.Pinned).ThenByDescending(r => r.Article.PublishedUtc).ToList();
        IReadOnlyList<NewsSummary> items = visible.Skip(skip).Take(take)
            .Select(r => new NewsSummary(r.Article.Slug, r.Article.Category, r.Article.Title, r.Article.Summary, r.Article.ImageUrl, r.Pinned, r.Article.PublishedUtc)).ToList();
        return Task.FromResult((items, visible.Count));
    }

    public Task<NewsArticle?> GetPublishedAsync(string slug, DateTimeOffset nowUtc, CancellationToken ct) =>
        Task.FromResult(Rows.Select(r => r.Article).FirstOrDefault(a => a.Slug == slug && a.PublishedUtc <= nowUtc));
}

internal sealed class MemoryEvents : IEventReader
{
    public List<EventRow> Rows { get; } = [];

    private static DateTimeOffset End(EventRow e) => e.EndsUtc ?? e.StartsUtc + ContentService.InstantEventDuration;

    public Task<IReadOnlyList<EventRow>> ListBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EventRow>>(Rows.Where(e => e.StartsUtc < toUtc && End(e) > fromUtc).OrderBy(e => e.StartsUtc).ToList());

    public Task<IReadOnlyList<EventRow>> ListStartingAfterAsync(DateTimeOffset fromUtc, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EventRow>>(Rows.Where(e => e.StartsUtc > fromUtc).OrderBy(e => e.StartsUtc).Take(take).ToList());

    public Task<IReadOnlyList<EventRow>> ListStartedBeforeAsync(DateTimeOffset beforeUtc, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EventRow>>(Rows.Where(e => e.StartsUtc <= beforeUtc).OrderByDescending(e => e.StartsUtc).Take(take).ToList());

    public Task<EventDetailRow?> GetAsync(string slug, CancellationToken ct)
    {
        var e = Rows.FirstOrDefault(r => r.Slug == slug);
        return Task.FromResult(e is null ? null : new EventDetailRow(e, "Descrição", "Regras", null, "manual", e.StartsUtc.AddDays(-1)));
    }
}

internal sealed class MemoryReminders : IEventReminderStore
{
    public HashSet<(int EventId, string Login)> Rows { get; } = [];

    public Task<bool> ExistsAsync(int eventId, string gameLogin, CancellationToken ct) => Task.FromResult(Rows.Contains((eventId, gameLogin)));

    public Task AddAsync(int eventId, string gameLogin, CancellationToken ct)
    {
        Rows.Add((eventId, gameLogin));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(int eventId, string gameLogin, CancellationToken ct)
    {
        Rows.Remove((eventId, gameLogin));
        return Task.CompletedTask;
    }

    public List<DueReminder> Due { get; } = [];

    public List<(int EventId, string Login)> Sent { get; } = [];

    public Task<IReadOnlyList<DueReminder>> ListDueAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DueReminder>>(Due.Where(d => d.StartsUtc > fromUtc && d.StartsUtc <= toUtc && !Sent.Contains((d.EventId, d.GameLogin))).ToList());

    public Task MarkSentAsync(int eventId, string gameLogin, DateTimeOffset sentUtc, CancellationToken ct)
    {
        Sent.Add((eventId, gameLogin));
        return Task.CompletedTask;
    }
}

internal sealed class MemoryAccounts : IPlayerAccountRepository
{
    public Dictionary<string, PlayerAccount> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<PlayerAccount?> FindAsync(string gameLogin, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault(gameLogin));

    public Task<bool> EmailInUseAsync(string normalizedEmail, string? exceptLogin, CancellationToken ct) => Task.FromResult(false);

    public Task UpsertAsync(string gameLogin, string? email, string? ipAddress, CancellationToken ct) => Task.CompletedTask;

    public Task MarkEmailConfirmedAsync(string gameLogin, DateTimeOffset at, CancellationToken ct) => Task.CompletedTask;

    public Task TouchLastLoginAsync(string gameLogin, DateTimeOffset at, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class CapturingEmail : IEmailSender
{
    public bool IsConfigured => true;

    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class ContentFixture : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero); // 12:00 em Brasília

    public FixedClock Clock { get; } = new(Now);
    public MemoryNews News { get; } = new();
    public MemoryEvents Events { get; } = new();
    public MemoryReminders Reminders { get; } = new();
    public MemoryAccounts Accounts { get; } = new();
    public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

    public ContentService Service() => new(
        News, Events, Reminders, Accounts, Cache, Clock,
        Options.Create(new ServerPresentationOptions { Name = "NexusPK" }),
        Options.Create(new AccountOptions { PublicBaseUrl = "https://portal.example.test" }));

    public static EventRow Event(int id, string slug, DateTimeOffset start, DateTimeOffset? end = null, bool cancelled = false) =>
        new(id, slug, "boss", $"Evento {id}", "Resumo", null, "Ricarten", start, end, cancelled);

    public void Dispose() => Cache.Dispose();
}

public class ContentServiceTests
{
    private static readonly DateTimeOffset Now = ContentFixture.Now;

    [Theory]
    [InlineData(-120, null, false, EventState.Ended)]   // sem fim, começou há 2 h
    [InlineData(-30, null, false, EventState.Live)]     // sem fim, dentro da 1 h
    [InlineData(30, null, false, EventState.Scheduled)]
    [InlineData(-600, 60, false, EventState.Live)]      // com fim no futuro
    [InlineData(-600, -1, false, EventState.Ended)]
    [InlineData(30, null, true, EventState.Cancelled)]
    public void State_follows_start_end_and_cancellation(int startMinutes, int? endMinutes, bool cancelled, EventState expected)
    {
        var e = ContentFixture.Event(1, "a", Now.AddMinutes(startMinutes), endMinutes is null ? null : Now.AddMinutes(endMinutes.Value), cancelled);
        Assert.Equal(expected, ContentService.StateOf(e, Now));
    }

    [Fact]
    public async Task Overview_splits_live_upcoming_and_recent()
    {
        using var f = new ContentFixture();
        f.Events.Rows.AddRange([
            ContentFixture.Event(1, "ao-vivo", Now.AddHours(-2), Now.AddHours(2)),
            ContentFixture.Event(2, "proximo", Now.AddHours(3)),
            ContentFixture.Event(3, "passado", Now.AddDays(-1)),
            ContentFixture.Event(4, "cancelado", Now.AddHours(-1), Now.AddHours(1), cancelled: true),
        ]);

        var o = await f.Service().GetOverviewAsync(CancellationToken.None);

        Assert.Equal(["ao-vivo"], o.Live.Select(e => e.Slug));
        Assert.Equal(["proximo"], o.Upcoming.Select(e => e.Slug));
        Assert.Equal(["cancelado", "passado"], o.Recent.Select(e => e.Slug));
        Assert.Equal(EventState.Cancelled, o.Recent[0].State);
    }

    [Fact]
    public async Task Calendar_uses_brasilia_month_bounds()
    {
        using var f = new ContentFixture();
        // 01/12 01:00 UTC = 30/11 22:00 em Brasília: pertence a novembro.
        f.Events.Rows.Add(ContentFixture.Event(1, "fim-de-novembro", new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero)));
        // 01/12 04:00 UTC = 01/12 01:00 em Brasília: pertence a dezembro.
        f.Events.Rows.Add(ContentFixture.Event(2, "inicio-de-dezembro", new DateTimeOffset(2026, 12, 1, 4, 0, 0, TimeSpan.Zero)));

        var nov = await f.Service().GetCalendarAsync(2026, 11, CancellationToken.None);
        var dec = await f.Service().GetCalendarAsync(2026, 12, CancellationToken.None);

        Assert.Equal(["fim-de-novembro"], nov.Events.Select(e => e.Slug));
        Assert.Equal(["inicio-de-dezembro"], dec.Events.Select(e => e.Slug));
    }

    [Fact]
    public async Task News_hides_scheduled_items_and_clamps_page_size()
    {
        using var f = new ContentFixture();
        f.News.Rows.Add((new NewsArticle("publicada", "noticia", "A", "r", "b", null, null, Now.AddDays(-1), Now), false));
        f.News.Rows.Add((new NewsArticle("agendada", "noticia", "B", "r", "b", null, null, Now.AddDays(1), Now), false));

        var page = await f.Service().ListNewsAsync(null, 0, 500, CancellationToken.None);

        Assert.Equal(["publicada"], page.Items.Select(n => n.Slug));
        Assert.Equal(1, page.Page);
        Assert.Equal(ContentService.MaxPageSize, page.PageSize);
        Assert.Null(await f.Service().GetNewsAsync("agendada", CancellationToken.None));
    }

    [Fact]
    public async Task Reminder_requires_confirmed_email_and_future_event()
    {
        using var f = new ContentFixture();
        f.Events.Rows.Add(ContentFixture.Event(1, "futuro", Now.AddHours(5)));
        f.Events.Rows.Add(ContentFixture.Event(2, "comecou", Now.AddMinutes(-10)));
        f.Accounts.Rows["semEmail"] = new PlayerAccount("semEmail", "a@b.test", null, Now, null);
        f.Accounts.Rows["ok"] = new PlayerAccount("ok", "c@d.test", Now, Now, null);
        var svc = f.Service();

        Assert.Equal(ReminderResult.EmailNotConfirmed, await svc.SetReminderAsync("semEmail", "futuro", true, CancellationToken.None));
        Assert.Equal(ReminderResult.NotScheduled, await svc.SetReminderAsync("ok", "comecou", true, CancellationToken.None));
        Assert.Equal(ReminderResult.NotFound, await svc.SetReminderAsync("ok", "nao-existe", true, CancellationToken.None));
        Assert.Equal(ReminderResult.Ok, await svc.SetReminderAsync("ok", "futuro", true, CancellationToken.None));
        Assert.True((await svc.GetEventAsync("futuro", "ok", CancellationToken.None))!.ReminderSet);
        Assert.Null((await svc.GetEventAsync("futuro", null, CancellationToken.None))!.ReminderSet);
        Assert.Equal(ReminderResult.Ok, await svc.SetReminderAsync("ok", "futuro", false, CancellationToken.None));
        Assert.Empty(f.Reminders.Rows);
    }

    [Fact]
    public async Task Ics_is_valid_utc_and_escapes_text()
    {
        using var f = new ContentFixture();
        f.Events.Rows.Add(new EventRow(7, "boss-semanal", "boss", "Boss; semanal, Valento", "Linha 1\nLinha 2", null, null,
            new DateTimeOffset(2026, 11, 14, 23, 0, 0, TimeSpan.Zero), null, false));

        var ics = await f.Service().GetIcsAsync("boss-semanal", CancellationToken.None);

        Assert.NotNull(ics);
        Assert.Contains("DTSTART:20261114T230000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261115T000000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:NexusPK: Boss\\; semanal\\, Valento\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DESCRIPTION:Linha 1\\nLinha 2\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("UID:evento-7@portal.example.test", ics, StringComparison.Ordinal);
        Assert.All(ics!.Split("\r\n"), l => Assert.True(System.Text.Encoding.UTF8.GetByteCount(l) <= 75));
    }

    [Fact]
    public void Fold_never_splits_a_multibyte_character()
    {
        var line = "DESCRIPTION:" + new string('ã', 60);
        var folded = ContentService.Fold(line);
        Assert.Equal(line, folded.Replace("\r\n ", string.Empty, StringComparison.Ordinal));
        Assert.All(folded.Split("\r\n"), l => Assert.True(System.Text.Encoding.UTF8.GetByteCount(l) <= 75));
    }

    [Fact]
    public async Task Reminder_job_sends_once_with_brasilia_time()
    {
        using var f = new ContentFixture();
        var mail = new CapturingEmail();
        f.Reminders.Due.Add(new DueReminder(1, "boss", "Boss", Now.AddMinutes(45), "ok", "c@d.test"));
        f.Reminders.Due.Add(new DueReminder(2, "depois", "Depois", Now.AddHours(3), "ok", "c@d.test"));
        var svc = new EventReminderService(f.Reminders, mail, f.Clock,
            Options.Create(new ServerPresentationOptions { Name = "NexusPK" }),
            Options.Create(new AccountOptions { PublicBaseUrl = "https://portal.example.test" }),
            NullLogger<EventReminderService>.Instance);

        Assert.Equal(1, await svc.SendDueAsync(CancellationToken.None));
        Assert.Equal(0, await svc.SendDueAsync(CancellationToken.None));
        var m = Assert.Single(mail.Sent);
        Assert.Equal("c@d.test", m.To);
        Assert.Contains("12:45", m.Subject, StringComparison.Ordinal);
        Assert.Contains("https://portal.example.test/eventos/boss", m.TextBody, StringComparison.Ordinal);
    }
}
