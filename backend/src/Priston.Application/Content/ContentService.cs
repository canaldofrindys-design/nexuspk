using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Home;
using Priston.Domain.Time;

namespace Priston.Application.Content;

public enum ReminderResult
{
    Ok,
    NotFound,
    NotScheduled,
    EmailNotConfirmed,
}

/// <summary>
/// Notícias e eventos publicados no portal. Só leitura para o público; os lembretes
/// são a única escrita feita pelo jogador.
/// Um evento aqui é divulgação: o site não executa nada dentro do jogo.
/// </summary>
public sealed class ContentService(
    INewsReader news,
    IEventReader events,
    IEventReminderStore reminders,
    IPlayerAccountRepository accounts,
    IMemoryCache cache,
    TimeProvider clock,
    IOptions<ServerPresentationOptions> presentation,
    IOptions<AccountOptions> accountOptions) : IDisposable
{
    public const int MaxPageSize = 50;

    /// <summary>Evento sem horário de fim: fica "em andamento" por esta duração após o início.</summary>
    public static readonly TimeSpan InstantEventDuration = TimeSpan.FromHours(1);

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(RankingPeriods.TimeZoneId);

    public Task<NewsPage> ListNewsAsync(string? category, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return Cached($"news:{category}:{page}:{pageSize}", async () =>
        {
            var (items, total) = await news.ListPublishedAsync(category, (page - 1) * pageSize, pageSize, clock.GetUtcNow(), ct).ConfigureAwait(false);
            return new NewsPage(items, total, page, pageSize);
        });
    }

    public Task<NewsArticle?> GetNewsAsync(string slug, CancellationToken ct) =>
        Cached($"news-item:{slug}", () => news.GetPublishedAsync(slug, clock.GetUtcNow(), ct));

    public Task<EventOverview> GetOverviewAsync(CancellationToken ct) => Cached("events:overview", async () =>
    {
        var now = clock.GetUtcNow();
        var live = await events.ListBetweenAsync(now, now.AddSeconds(1), ct).ConfigureAwait(false);
        var upcoming = await events.ListStartingAfterAsync(now, 20, ct).ConfigureAwait(false);
        var past = await events.ListStartedBeforeAsync(now, 30, ct).ConfigureAwait(false);
        return new EventOverview(
            live.Where(e => !e.Cancelled).Select(e => Summarize(e, now)).OrderBy(e => e.StartsUtc).ToList(),
            upcoming.Select(e => Summarize(e, now)).ToList(),
            past.Select(e => Summarize(e, now)).Where(e => e.State is EventState.Ended or EventState.Cancelled).Take(10).ToList(),
            now);
    });

    /// <summary>Eventos que tocam o mês (horário de Brasília).</summary>
    public Task<EventCalendar> GetCalendarAsync(int year, int month, CancellationToken ct) => Cached($"events:cal:{year}:{month}", async () =>
    {
        var now = clock.GetUtcNow();
        var startLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var from = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, Zone));
        var to = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal.AddMonths(1), Zone));
        var rows = await events.ListBetweenAsync(from, to, ct).ConfigureAwait(false);
        return new EventCalendar(year, month, rows.Select(e => Summarize(e, now)).OrderBy(e => e.StartsUtc).ToList());
    });

    /// <summary>Detalhe do evento. Com login, informa se o jogador pediu lembrete.</summary>
    public async Task<EventDetail?> GetEventAsync(string slug, string? gameLogin, CancellationToken ct)
    {
        var row = await Cached($"event:{slug}", () => events.GetAsync(slug, ct)).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        bool? reminder = gameLogin is null ? null : await reminders.ExistsAsync(row.Event.Id, gameLogin, ct).ConfigureAwait(false);
        return new EventDetail(Summarize(row.Event, clock.GetUtcNow()), row.Description, row.Rules, row.Prizes, row.ExecutionMode, row.UpdatedUtc, reminder);
    }

    public async Task<ReminderResult> SetReminderAsync(string gameLogin, string slug, bool enabled, CancellationToken ct)
    {
        var row = await events.GetAsync(slug, ct).ConfigureAwait(false);
        if (row is null)
        {
            return ReminderResult.NotFound;
        }

        if (!enabled)
        {
            await reminders.RemoveAsync(row.Event.Id, gameLogin, ct).ConfigureAwait(false);
            return ReminderResult.Ok;
        }

        if (StateOf(row.Event, clock.GetUtcNow()) != EventState.Scheduled)
        {
            return ReminderResult.NotScheduled;
        }

        var account = await accounts.FindAsync(gameLogin, ct).ConfigureAwait(false);
        if (account?.Email is null || account.EmailConfirmedUtc is null)
        {
            return ReminderResult.EmailNotConfirmed;
        }

        await reminders.AddAsync(row.Event.Id, gameLogin, ct).ConfigureAwait(false);
        return ReminderResult.Ok;
    }

    /// <summary>Arquivo iCalendar (RFC 5545) para o jogador adicionar o evento à própria agenda.</summary>
    public async Task<string?> GetIcsAsync(string slug, CancellationToken ct)
    {
        var row = await Cached($"event:{slug}", () => events.GetAsync(slug, ct)).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var e = row.Event;
        var baseUrl = accountOptions.Value.PublicBaseUrl.TrimEnd('/');
        var host = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Host : "portal";
        var sb = new StringBuilder();
        void Line(string text) => sb.Append(Fold(text)).Append("\r\n");
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line($"PRODID:-//{Escape(presentation.Value.Name)}//Portal//PT-BR");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line("BEGIN:VEVENT");
        Line($"UID:evento-{e.Id}@{host}");
        Line($"DTSTAMP:{IcsTime(row.UpdatedUtc)}");
        Line($"DTSTART:{IcsTime(e.StartsUtc)}");
        Line($"DTEND:{IcsTime(e.EndsUtc ?? e.StartsUtc + InstantEventDuration)}");
        Line($"SUMMARY:{Escape($"{presentation.Value.Name}: {e.Name}")}");
        Line($"DESCRIPTION:{Escape(e.Summary)}");
        if (!string.IsNullOrWhiteSpace(e.Location))
        {
            Line($"LOCATION:{Escape(e.Location)}");
        }

        Line($"URL:{baseUrl}/eventos/{e.Slug}");
        Line(e.Cancelled ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
        Line("END:VEVENT");
        Line("END:VCALENDAR");
        return sb.ToString();
    }

    public static EventState StateOf(EventRow e, DateTimeOffset now)
    {
        if (e.Cancelled)
        {
            return EventState.Cancelled;
        }

        if (now < e.StartsUtc)
        {
            return EventState.Scheduled;
        }

        var end = e.EndsUtc ?? e.StartsUtc + InstantEventDuration;
        return now < end ? EventState.Live : EventState.Ended;
    }

    public static EventSummary Summarize(EventRow e, DateTimeOffset now) =>
        new(e.Slug, e.Category, e.Name, e.Summary, e.ImageUrl, e.Location, e.StartsUtc, e.EndsUtc, StateOf(e, now));

    private CancellationTokenSource _generation = new();

    public void Dispose() => _generation.Dispose();

    /// <summary>Descarta o cache de notícias e eventos (chamado depois de uma gravação no painel).</summary>
    public void Invalidate()
    {
        var old = Interlocked.Exchange(ref _generation, new CancellationTokenSource());
        // Sem Dispose: uma leitura em andamento ainda pode pegar o token antigo.
        old.Cancel();
    }

    private Task<T> Cached<T>(string key, Func<Task<T>> factory) =>
        cache.GetOrCreateAsync("content:" + key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            entry.AddExpirationToken(new CancellationChangeToken(_generation.Token));
            return factory();
        })!;

    private static string IcsTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    public static string Escape(string text) => text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", string.Empty, StringComparison.Ordinal);

    /// <summary>Linhas de no máximo 75 octetos (RFC 5545 §3.1), sem cortar caractere UTF-8 ao meio.</summary>
    public static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= 75)
        {
            return line;
        }

        var sb = new StringBuilder();
        var bytes = 0;
        var limit = 75;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > limit)
            {
                sb.Append("\r\n ");
                bytes = 1;
                limit = 75;
            }

            sb.Append(rune.ToString());
            bytes += size;
        }

        return sb.ToString();
    }
}
