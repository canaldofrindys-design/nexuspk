using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Content;
using Priston.Infrastructure.Accounts;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Content;

internal sealed class NewsReader(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : INewsReader
{
    public async Task<(IReadOnlyList<NewsSummary> Items, int Total)> ListPublishedAsync(string? category, int skip, int take, DateTimeOffset nowUtc, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT COUNT(*) FROM dbo.News
            WHERE Status = 'published' AND PublishedUtc <= @now AND (@category IS NULL OR Category = @category);

            SELECT Slug, Category, Title, Summary, ImageUrl, Pinned, PublishedUtc
            FROM dbo.News
            WHERE Status = 'published' AND PublishedUtc <= @now AND (@category IS NULL OR Category = @category)
            ORDER BY Pinned DESC, PublishedUtc DESC, Id DESC
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new { now = nowUtc.UtcDateTime, category = Ansi(category, 16), skip, take },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<NewsRow>().ConfigureAwait(false);
        return (rows.Select(r => new NewsSummary(r.Slug, r.Category, r.Title, r.Summary, r.ImageUrl, r.Pinned, Utc.To(r.PublishedUtc))).ToList(), total);
    }

    public async Task<NewsArticle?> GetPublishedAsync(string slug, DateTimeOffset nowUtc, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<NewsRow>(new CommandDefinition(
            """
            SELECT Slug, Category, Title, Summary, Body, ImageUrl, AuthorName, Pinned, PublishedUtc, UpdatedUtc
            FROM dbo.News
            WHERE Slug = @slug AND Status = 'published' AND PublishedUtc <= @now
            """,
            new { slug = Ansi(slug, 120), now = nowUtc.UtcDateTime },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return r is null
            ? null
            : new NewsArticle(r.Slug, r.Category, r.Title, r.Summary, r.Body ?? string.Empty, r.ImageUrl, r.AuthorName, Utc.To(r.PublishedUtc), Utc.To(r.UpdatedUtc));
    }

    internal static DbString Ansi(string? value, int length) => new() { Value = value, IsAnsi = true, Length = length };

    private sealed class NewsRow
    {
        public string Slug { get; init; } = "";
        public string Category { get; init; } = "";
        public string Title { get; init; } = "";
        public string Summary { get; init; } = "";
        public string? Body { get; init; }
        public string? ImageUrl { get; init; }
        public string? AuthorName { get; init; }
        public bool Pinned { get; init; }
        public DateTime PublishedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
    }
}

internal sealed class EventReader(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IEventReader
{
    private const string Columns = "Id, Slug, Category, Name, Summary, ImageUrl, Location, StartsUtc, EndsUtc, Status";

    // Evento sem fim ocupa a mesma janela que ContentService.InstantEventDuration.
    private static readonly int InstantMinutes = (int)ContentService.InstantEventDuration.TotalMinutes;

    public Task<IReadOnlyList<EventRow>> ListBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) => Query(
        $"""
        SELECT {Columns} FROM dbo.GameEvent
        WHERE Status IN ('published','cancelled')
          AND StartsUtc < @to
          AND COALESCE(EndsUtc, DATEADD(minute, @instant, StartsUtc)) > @from
        ORDER BY StartsUtc, Id
        """,
        new { from = fromUtc.UtcDateTime, to = toUtc.UtcDateTime, instant = InstantMinutes },
        ct);

    public Task<IReadOnlyList<EventRow>> ListStartingAfterAsync(DateTimeOffset fromUtc, int take, CancellationToken ct) => Query(
        $"""
        SELECT TOP (@take) {Columns} FROM dbo.GameEvent
        WHERE Status IN ('published','cancelled') AND StartsUtc > @from
        ORDER BY StartsUtc, Id
        """,
        new { from = fromUtc.UtcDateTime, take },
        ct);

    public Task<IReadOnlyList<EventRow>> ListStartedBeforeAsync(DateTimeOffset beforeUtc, int take, CancellationToken ct) => Query(
        $"""
        SELECT TOP (@take) {Columns} FROM dbo.GameEvent
        WHERE Status IN ('published','cancelled') AND StartsUtc <= @before
        ORDER BY StartsUtc DESC, Id DESC
        """,
        new { before = beforeUtc.UtcDateTime, take },
        ct);

    public async Task<EventDetailRow?> GetAsync(string slug, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<Row>(new CommandDefinition(
            $"""
            SELECT {Columns}, Description, Rules, Prizes, ExecutionMode, UpdatedUtc
            FROM dbo.GameEvent
            WHERE Slug = @slug AND Status IN ('published','cancelled')
            """,
            new { slug = NewsReader.Ansi(slug, 120) },
            commandTimeout: options.Value.CommandTimeoutSeconds,
            cancellationToken: ct)).ConfigureAwait(false);
        return r is null ? null : new EventDetailRow(ToEvent(r), r.Description ?? string.Empty, r.Rules, r.Prizes, r.ExecutionMode ?? "manual", Utc.To(r.UpdatedUtc));
    }

    private async Task<IReadOnlyList<EventRow>> Query(string sql, object args, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(sql, args, commandTimeout: options.Value.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(ToEvent).ToList();
    }

    private static EventRow ToEvent(Row r) => new(
        r.Id, r.Slug, r.Category, r.Name, r.Summary, r.ImageUrl, r.Location,
        Utc.To(r.StartsUtc), Utc.To(r.EndsUtc), string.Equals(r.Status, "cancelled", StringComparison.Ordinal));

    private sealed class Row
    {
        public int Id { get; init; }
        public string Slug { get; init; } = "";
        public string Category { get; init; } = "";
        public string Name { get; init; } = "";
        public string Summary { get; init; } = "";
        public string? ImageUrl { get; init; }
        public string? Location { get; init; }
        public DateTime StartsUtc { get; init; }
        public DateTime? EndsUtc { get; init; }
        public string Status { get; init; } = "";
        public string? Description { get; init; }
        public string? Rules { get; init; }
        public string? Prizes { get; init; }
        public string? ExecutionMode { get; init; }
        public DateTime UpdatedUtc { get; init; }
    }
}

internal sealed class EventReminderStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : IEventReminderStore
{
    private int Timeout => options.Value.CommandTimeoutSeconds;

    public async Task<bool> ExistsAsync(int eventId, string gameLogin, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM dbo.GameEventReminder WHERE EventId = @eventId AND GameLogin = @login",
            new { eventId, login = NewsReader.Ansi(gameLogin, 32) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false) is not null;
    }

    public async Task AddAsync(int eventId, string gameLogin, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.GameEventReminder WITH (UPDLOCK, HOLDLOCK) WHERE EventId = @eventId AND GameLogin = @login)
                INSERT INTO dbo.GameEventReminder (EventId, GameLogin) VALUES (@eventId, @login);
            """,
            new { eventId, login = NewsReader.Ansi(gameLogin, 32) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task RemoveAsync(int eventId, string gameLogin, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.GameEventReminder WHERE EventId = @eventId AND GameLogin = @login",
            new { eventId, login = NewsReader.Ansi(gameLogin, 32) }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DueReminder>> ListDueAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int take, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<DueRow>(new CommandDefinition(
            """
            SELECT TOP (@take) e.Id AS EventId, e.Slug, e.Name, e.StartsUtc, r.GameLogin, a.Email
            FROM dbo.GameEventReminder r
            JOIN dbo.GameEvent e ON e.Id = r.EventId
            JOIN dbo.PlayerAccount a ON a.GameLogin = r.GameLogin
            WHERE r.SentUtc IS NULL
              AND e.Status = 'published'
              AND e.StartsUtc > @from AND e.StartsUtc <= @to
              AND a.Email IS NOT NULL AND a.EmailConfirmedUtc IS NOT NULL
            ORDER BY e.StartsUtc
            """,
            new { from = fromUtc.UtcDateTime, to = toUtc.UtcDateTime, take }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new DueReminder(r.EventId, r.Slug, r.Name, Utc.To(r.StartsUtc), r.GameLogin, r.Email)).ToList();
    }

    public async Task MarkSentAsync(int eventId, string gameLogin, DateTimeOffset sentUtc, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.GameEventReminder SET SentUtc = @sent WHERE EventId = @eventId AND GameLogin = @login AND SentUtc IS NULL",
            new { eventId, login = NewsReader.Ansi(gameLogin, 32), sent = sentUtc.UtcDateTime }, commandTimeout: Timeout, cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed class DueRow
    {
        public int EventId { get; init; }
        public string Slug { get; init; } = "";
        public string Name { get; init; } = "";
        public DateTime StartsUtc { get; init; }
        public string GameLogin { get; init; } = "";
        public string Email { get; init; } = "";
    }
}
