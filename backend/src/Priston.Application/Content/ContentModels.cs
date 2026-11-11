namespace Priston.Application.Content;

/// <summary>Categorias de notícia aceitas pela tabela dbo.News.</summary>
public static class NewsCategories
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["noticia"] = "Notícia",
        ["atualizacao"] = "Atualização",
        ["manutencao"] = "Manutenção",
        ["evento"] = "Evento",
    };

    public static bool IsValid(string? code) => code is not null && Labels.ContainsKey(code);
}

/// <summary>Categorias de evento aceitas pela tabela dbo.GameEvent.</summary>
public static class EventCategories
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["exp"] = "EXP",
        ["drop"] = "Drop",
        ["boss"] = "Boss",
        ["pvp"] = "PvP",
        ["invasao"] = "Invasão",
        ["especial"] = "Especial",
        ["comemorativo"] = "Comemorativo",
        ["gm"] = "Evento de GM",
    };

    public static bool IsValid(string? code) => code is not null && Labels.ContainsKey(code);
}

public enum EventState
{
    Scheduled,
    Live,
    Ended,
    Cancelled,
}

public sealed record NewsSummary(string Slug, string Category, string Title, string Summary, string? ImageUrl, bool Pinned, DateTimeOffset PublishedUtc);

public sealed record NewsArticle(
    string Slug,
    string Category,
    string Title,
    string Summary,
    string Body,
    string? ImageUrl,
    string? AuthorName,
    DateTimeOffset PublishedUtc,
    DateTimeOffset UpdatedUtc);

/// <summary>Linha de evento como está no banco (status 'published' ou 'cancelled').</summary>
public sealed record EventRow(
    int Id,
    string Slug,
    string Category,
    string Name,
    string Summary,
    string? ImageUrl,
    string? Location,
    DateTimeOffset StartsUtc,
    DateTimeOffset? EndsUtc,
    bool Cancelled);

public sealed record EventDetailRow(EventRow Event, string Description, string? Rules, string? Prizes, string ExecutionMode, DateTimeOffset UpdatedUtc);

public sealed record EventSummary(
    string Slug,
    string Category,
    string Name,
    string Summary,
    string? ImageUrl,
    string? Location,
    DateTimeOffset StartsUtc,
    DateTimeOffset? EndsUtc,
    EventState State);

public sealed record EventDetail(
    EventSummary Event,
    string Description,
    string? Rules,
    string? Prizes,
    string ExecutionMode,
    DateTimeOffset UpdatedUtc,
    bool? ReminderSet);

public sealed record EventOverview(
    IReadOnlyList<EventSummary> Live,
    IReadOnlyList<EventSummary> Upcoming,
    IReadOnlyList<EventSummary> Recent,
    DateTimeOffset GeneratedUtc);

public sealed record EventCalendar(int Year, int Month, IReadOnlyList<EventSummary> Events);

public sealed record NewsPage(IReadOnlyList<NewsSummary> Items, int Total, int Page, int PageSize);

/// <summary>Lembrete devido: o jogador pediu aviso e o evento começa em breve.</summary>
public sealed record DueReminder(int EventId, string Slug, string EventName, DateTimeOffset StartsUtc, string GameLogin, string Email);

public interface INewsReader
{
    Task<(IReadOnlyList<NewsSummary> Items, int Total)> ListPublishedAsync(string? category, int skip, int take, DateTimeOffset nowUtc, CancellationToken ct);

    Task<NewsArticle?> GetPublishedAsync(string slug, DateTimeOffset nowUtc, CancellationToken ct);
}

public interface IEventReader
{
    /// <summary>Eventos publicados ou cancelados que tocam a janela [fromUtc, toUtc).</summary>
    Task<IReadOnlyList<EventRow>> ListBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>Próximos eventos: começam a partir de fromUtc, em ordem de início.</summary>
    Task<IReadOnlyList<EventRow>> ListStartingAfterAsync(DateTimeOffset fromUtc, int take, CancellationToken ct);

    /// <summary>Eventos que começaram antes de beforeUtc, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<EventRow>> ListStartedBeforeAsync(DateTimeOffset beforeUtc, int take, CancellationToken ct);

    Task<EventDetailRow?> GetAsync(string slug, CancellationToken ct);
}

public interface IEventReminderStore
{
    Task<bool> ExistsAsync(int eventId, string gameLogin, CancellationToken ct);

    Task AddAsync(int eventId, string gameLogin, CancellationToken ct);

    Task RemoveAsync(int eventId, string gameLogin, CancellationToken ct);

    /// <summary>Lembretes não enviados de eventos publicados que começam entre fromUtc e toUtc, só de contas com e-mail confirmado.</summary>
    Task<IReadOnlyList<DueReminder>> ListDueAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int take, CancellationToken ct);

    Task MarkSentAsync(int eventId, string gameLogin, DateTimeOffset sentUtc, CancellationToken ct);
}
