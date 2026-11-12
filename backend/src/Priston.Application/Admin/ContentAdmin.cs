using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Priston.Application.Accounts;
using Priston.Application.Content;
using Priston.Domain.Time;

namespace Priston.Application.Admin;

/// <summary>Datas vêm do formulário em horário de Brasília ("AAAA-MM-DDTHH:MM") e são gravadas em UTC.</summary>
public sealed record NewsInput(string? Slug, string? Category, string? Title, string? Summary, string? Body, string? ImageUrl, bool Pinned, string? Status, string? PublishedLocal);

public sealed record EventInput(
    string? Slug,
    string? Category,
    string? Name,
    string? Summary,
    string? Description,
    string? Rules,
    string? Prizes,
    string? Location,
    string? ImageUrl,
    string? StartsLocal,
    string? EndsLocal,
    string? Status);

public sealed record NewsRecord(int Id, string Slug, string Category, string Title, string Summary, string Body, string? ImageUrl, bool Pinned, string Status,
    DateTimeOffset? PublishedUtc, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, string? CreatedBy, string? UpdatedBy);

public sealed record EventRecord(int Id, string Slug, string Category, string Name, string Summary, string Description, string? Rules, string? Prizes,
    string? Location, string? ImageUrl, DateTimeOffset StartsUtc, DateTimeOffset? EndsUtc, string Status, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc,
    string? CreatedBy, string? UpdatedBy);

public sealed record AdminList<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record SaveResult(StaffAuthStatus Status, int? Id = null, IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>Dados já validados para gravar.</summary>
public sealed record NewsData(string Slug, string Category, string Title, string Summary, string Body, string? ImageUrl, bool Pinned, string Status, DateTimeOffset? PublishedUtc);

public sealed record EventData(string Slug, string Category, string Name, string Summary, string Description, string? Rules, string? Prizes, string? Location,
    string? ImageUrl, DateTimeOffset StartsUtc, DateTimeOffset? EndsUtc, string Status);

public interface IContentAdminStore
{
    Task<(IReadOnlyList<NewsRecord> Items, int Total)> ListNewsAsync(string? status, int skip, int take, CancellationToken ct);

    Task<NewsRecord?> GetNewsAsync(int id, CancellationToken ct);

    /// <summary>Devolve o Id, ou null se o slug já existe.</summary>
    Task<int?> CreateNewsAsync(NewsData data, string by, CancellationToken ct);

    /// <summary>False se o slug já existe em outra notícia.</summary>
    Task<bool> UpdateNewsAsync(int id, NewsData data, string by, CancellationToken ct);

    Task<(IReadOnlyList<EventRecord> Items, int Total)> ListEventsAsync(string? status, int skip, int take, CancellationToken ct);

    Task<EventRecord?> GetEventAsync(int id, CancellationToken ct);

    Task<int?> CreateEventAsync(EventData data, string by, CancellationToken ct);

    Task<bool> UpdateEventAsync(int id, EventData data, string by, CancellationToken ct);

    /// <summary>False se a notícia não existe.</summary>
    Task<bool> DeleteNewsAsync(int id, CancellationToken ct);

    /// <summary>Apaga o evento e os lembretes dele. False se não existe.</summary>
    Task<bool> DeleteEventAsync(int id, CancellationToken ct);

    /// <summary>Muda só o horário de fim (encerrar agora).</summary>
    Task EndEventAsync(int id, DateTimeOffset endsUtc, string by, CancellationToken ct);
}

/// <summary>Cadastro de notícias e eventos pela equipe. Toda gravação vai para a auditoria.</summary>
public sealed partial class ContentAdminService(IContentAdminStore store, ContentService content, IAuditLog audit, TimeProvider clock)
{
    public const int PageSize = 25;
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(RankingPeriods.TimeZoneId);
    private static readonly string[] NewsStatuses = ["draft", "published", "archived"];
    private static readonly string[] EventStatuses = ["draft", "published", "cancelled"];

    public async Task<AdminList<NewsRecord>> ListNewsAsync(string? status, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var (items, total) = await store.ListNewsAsync(NewsStatuses.Contains(status) ? status : null, (page - 1) * PageSize, PageSize, ct).ConfigureAwait(false);
        return new(items, total, page, PageSize);
    }

    public Task<NewsRecord?> GetNewsAsync(int id, CancellationToken ct) => store.GetNewsAsync(id, ct);

    public async Task<AdminList<EventRecord>> ListEventsAsync(string? status, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var (items, total) = await store.ListEventsAsync(EventStatuses.Contains(status) ? status : null, (page - 1) * PageSize, PageSize, ct).ConfigureAwait(false);
        return new(items, total, page, PageSize);
    }

    public Task<EventRecord?> GetEventAsync(int id, CancellationToken ct) => store.GetEventAsync(id, ct);

    public async Task<SaveResult> SaveNewsAsync(StaffPrincipal me, int? id, NewsInput input, ClientInfo client, CancellationToken ct)
    {
        NewsRecord? before = null;
        if (id is not null && (before = await store.GetNewsAsync(id.Value, ct).ConfigureAwait(false)) is null)
        {
            return new(StaffAuthStatus.NotFound);
        }

        var errors = new Dictionary<string, string>();
        var slug = Slug(input.Slug, errors);
        if (!NewsCategories.IsValid(input.Category))
        {
            errors["category"] = "Escolha uma categoria.";
        }

        var title = Text(input.Title, 160, "title", errors, required: true)!;
        var summary = Text(input.Summary, 400, "summary", errors, required: true)!;
        var body = Text(input.Body, 50_000, "body", errors, required: true)!;
        var image = Image(input.ImageUrl, errors);
        var status = NewsStatuses.Contains(input.Status) ? input.Status! : null;
        if (status is null)
        {
            errors["status"] = "Escolha a situação.";
        }

        var published = Local(input.PublishedLocal, "publishedLocal", errors);
        if (status == "published" && published is null && !errors.ContainsKey("publishedLocal"))
        {
            // Publicar sem data: mantém a data original ou usa agora.
            // Truncado ao segundo: a coluna é datetime2(0) e arredondaria para cima, deixando a notícia "no futuro" por até 1 s.
            var now = clock.GetUtcNow();
            published = before?.PublishedUtc ?? now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        }

        if (errors.Count > 0)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        var data = new NewsData(slug!, input.Category!, title, summary, body, image, input.Pinned, status!, published);
        int? savedId = id;
        if (id is null)
        {
            savedId = await store.CreateNewsAsync(data, me.Username, ct).ConfigureAwait(false);
        }
        else if (!await store.UpdateNewsAsync(id.Value, data, me.Username, ct).ConfigureAwait(false))
        {
            savedId = null;
        }

        if (savedId is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, new Dictionary<string, string> { ["slug"] = "Já existe outra notícia com este endereço." });
        }

        await audit.WriteAsync("staff", me.Username, id is null ? "news.created" : "news.updated", "news", savedId.Value.ToString(CultureInfo.InvariantCulture),
            client.IpAddress, JsonSerializer.Serialize(new { data.Slug, data.Title, data.Status, data.PublishedUtc, beforeStatus = before?.Status }), ct).ConfigureAwait(false);
        content.Invalidate();
        return new(StaffAuthStatus.Ok, savedId);
    }

    public async Task<SaveResult> SaveEventAsync(StaffPrincipal me, int? id, EventInput input, ClientInfo client, CancellationToken ct)
    {
        EventRecord? before = null;
        if (id is not null && (before = await store.GetEventAsync(id.Value, ct).ConfigureAwait(false)) is null)
        {
            return new(StaffAuthStatus.NotFound);
        }

        var errors = new Dictionary<string, string>();
        var slug = Slug(input.Slug, errors);
        if (!EventCategories.IsValid(input.Category))
        {
            errors["category"] = "Escolha uma categoria.";
        }

        var name = Text(input.Name, 120, "name", errors, required: true)!;
        var summary = Text(input.Summary, 400, "summary", errors, required: true)!;
        var description = Text(input.Description, 50_000, "description", errors, required: true)!;
        var rules = Text(input.Rules, 50_000, "rules", errors, required: false);
        var prizes = Text(input.Prizes, 20_000, "prizes", errors, required: false);
        var location = Text(input.Location, 120, "location", errors, required: false);
        var image = Image(input.ImageUrl, errors);
        var status = EventStatuses.Contains(input.Status) ? input.Status! : null;
        if (status is null)
        {
            errors["status"] = "Escolha a situação.";
        }

        var starts = Local(input.StartsLocal, "startsLocal", errors);
        if (starts is null && !errors.ContainsKey("startsLocal"))
        {
            errors["startsLocal"] = "Informe o início.";
        }

        var ends = Local(input.EndsLocal, "endsLocal", errors);
        if (starts is not null && ends is not null && ends <= starts)
        {
            errors["endsLocal"] = "O fim precisa ser depois do início.";
        }

        if (errors.Count > 0)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        var data = new EventData(slug!, input.Category!, name, summary, description, rules, prizes, location, image, starts!.Value, ends, status!);
        int? savedId = id;
        if (id is null)
        {
            savedId = await store.CreateEventAsync(data, me.Username, ct).ConfigureAwait(false);
        }
        else if (!await store.UpdateEventAsync(id.Value, data, me.Username, ct).ConfigureAwait(false))
        {
            savedId = null;
        }

        if (savedId is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, new Dictionary<string, string> { ["slug"] = "Já existe outro evento com este endereço." });
        }

        await audit.WriteAsync("staff", me.Username, id is null ? "event.created" : "event.updated", "event", savedId.Value.ToString(CultureInfo.InvariantCulture),
            client.IpAddress, JsonSerializer.Serialize(new { data.Slug, data.Name, data.Status, data.StartsUtc, data.EndsUtc, beforeStatus = before?.Status, beforeStartsUtc = before?.StartsUtc }), ct).ConfigureAwait(false);
        content.Invalidate();
        return new(StaffAuthStatus.Ok, savedId);
    }

    public async Task<SaveResult> DeleteNewsAsync(StaffPrincipal me, int id, ClientInfo client, CancellationToken ct)
    {
        var before = await store.GetNewsAsync(id, ct).ConfigureAwait(false);
        if (before is null || !await store.DeleteNewsAsync(id, ct).ConfigureAwait(false))
        {
            return new(StaffAuthStatus.NotFound);
        }

        await audit.WriteAsync("staff", me.Username, "news.deleted", "news", id.ToString(CultureInfo.InvariantCulture), client.IpAddress,
            JsonSerializer.Serialize(new { before.Slug, before.Title, before.Status }), ct).ConfigureAwait(false);
        content.Invalidate();
        return new(StaffAuthStatus.Ok, id);
    }

    public async Task<SaveResult> DeleteEventAsync(StaffPrincipal me, int id, ClientInfo client, CancellationToken ct)
    {
        var before = await store.GetEventAsync(id, ct).ConfigureAwait(false);
        if (before is null || !await store.DeleteEventAsync(id, ct).ConfigureAwait(false))
        {
            return new(StaffAuthStatus.NotFound);
        }

        await audit.WriteAsync("staff", me.Username, "event.deleted", "event", id.ToString(CultureInfo.InvariantCulture), client.IpAddress,
            JsonSerializer.Serialize(new { before.Slug, before.Name, before.Status, before.StartsUtc }), ct).ConfigureAwait(false);
        content.Invalidate();
        return new(StaffAuthStatus.Ok, id);
    }

    /// <summary>
    /// Encerra agora um evento publicado que já começou: o fim passa a ser este minuto e o site
    /// deixa de mostrar o evento como em andamento. Evento que ainda não começou se cancela pela situação.
    /// </summary>
    public async Task<SaveResult> EndEventAsync(StaffPrincipal me, int id, ClientInfo client, CancellationToken ct)
    {
        var e = await store.GetEventAsync(id, ct).ConfigureAwait(false);
        if (e is null)
        {
            return new(StaffAuthStatus.NotFound);
        }

        var now = clock.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        var error = e.Status != "published" ? "Só um evento publicado pode ser encerrado."
            : now <= e.StartsUtc ? "O evento ainda não começou. Para desmarcar, mude a situação para Cancelado."
            : (e.EndsUtc ?? e.StartsUtc + ContentService.InstantEventDuration) <= now ? "Este evento já terminou."
            : null;
        if (error is not null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, new Dictionary<string, string> { ["end"] = error });
        }

        await store.EndEventAsync(id, now, me.Username, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "event.ended", "event", id.ToString(CultureInfo.InvariantCulture), client.IpAddress,
            JsonSerializer.Serialize(new { e.Slug, e.Name, beforeEndsUtc = e.EndsUtc, endsUtc = now }), ct).ConfigureAwait(false);
        content.Invalidate();
        return new(StaffAuthStatus.Ok, id);
    }

    /// <summary>Converte "AAAA-MM-DDTHH:MM" (Brasília) para UTC. Vazio é null.</summary>
    public static DateTimeOffset? ParseLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParseExact(value.Trim(), ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || local.Year is < 2020 or > 2100)
        {
            throw new FormatException();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone));
    }

    internal static DateTimeOffset? Local(string? value, string field, Dictionary<string, string> errors)
    {
        try
        {
            return ParseLocal(value);
        }
        catch (FormatException)
        {
            errors[field] = "Data inválida.";
            return null;
        }
    }

    private static string? Slug(string? value, Dictionary<string, string> errors)
    {
        var slug = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (slug.Length is 0 or > 120 || !SlugPattern().IsMatch(slug))
        {
            errors["slug"] = "Use letras minúsculas, números e hífen (ex.: evento-de-natal).";
            return null;
        }

        return slug;
    }

    internal static string? Text(string? value, int max, string field, Dictionary<string, string> errors, bool required)
    {
        var text = value?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (required)
            {
                errors[field] = "Campo obrigatório.";
            }

            return required ? string.Empty : null;
        }

        if (text.Length > max)
        {
            errors[field] = $"Use no máximo {max.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))} caracteres.";
        }

        return text;
    }

    /// <summary>Imagem ou link do próprio site ("/...") ou https. Nada de javascript:, data: ou http.</summary>
    internal static string? Image(string? value, Dictionary<string, string> errors, string field = "imageUrl")
    {
        var url = value?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var ok = url.Length <= 400 && (
            (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.Contains('\\', StringComparison.Ordinal))
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)));
        if (!ok)
        {
            errors[field] = "Use um caminho do site (/imagens/...) ou um endereço https.";
            return null;
        }

        return url;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
