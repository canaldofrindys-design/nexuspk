using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Home;
using Priston.Application.Support;

namespace Priston.Application.Site;

public sealed record RateItem(string Label, string Value);

public sealed record LinkItem(string Label, string Url);

/// <summary>Informações institucionais: nome, slogan, texto da página "O Servidor", taxas e comunidade.</summary>
public sealed record ServerSettings(string Name, string Tagline, string About, IReadOnlyList<RateItem> Rates, IReadOnlyList<LinkItem> CommunityLinks);

public sealed record DownloadItem(string Name, string? Description, string Url, string? Size, string? Version);

/// <summary>Página de download: texto de abertura, requisitos e arquivos (instalador, patch, launcher).</summary>
public sealed record DownloadSettings(string Intro, string Requirements, IReadOnlyList<DownloadItem> Items);

public sealed record PublicBanner(int Id, string Title, string? Subtitle, string? ImageUrl, string? LinkUrl, string? LinkLabel);

public sealed record SiteSnapshot(ServerSettings Server, DownloadSettings Downloads, IReadOnlyList<PublicBanner> Banners, DateTimeOffset? ServerUpdatedUtc, DateTimeOffset? DownloadsUpdatedUtc);

public sealed record SiteSettingRow(string Value, DateTimeOffset UpdatedUtc, string? UpdatedBy);

public sealed record BannerRecord(int Id, string Title, string? Subtitle, string? ImageUrl, string? LinkUrl, string? LinkLabel, int SortOrder, bool Active,
    DateTimeOffset? StartsUtc, DateTimeOffset? EndsUtc, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, string? CreatedBy, string? UpdatedBy);

public sealed record BannerData(string Title, string? Subtitle, string? ImageUrl, string? LinkUrl, string? LinkLabel, int SortOrder, bool Active,
    DateTimeOffset? StartsUtc, DateTimeOffset? EndsUtc);

// Entradas do painel (tudo opcional: a validação diz o que falta).
public sealed record RateInput(string? Label, string? Value);

public sealed record LinkInput(string? Label, string? Url);

public sealed record ServerSettingsInput(string? Name, string? Tagline, string? About, IReadOnlyList<RateInput>? Rates, IReadOnlyList<LinkInput>? CommunityLinks);

public sealed record DownloadItemInput(string? Name, string? Description, string? Url, string? Size, string? Version);

public sealed record DownloadSettingsInput(string? Intro, string? Requirements, IReadOnlyList<DownloadItemInput>? Items);

public sealed record BannerInput(string? Title, string? Subtitle, string? ImageUrl, string? LinkUrl, string? LinkLabel, int? SortOrder, bool Active, string? StartsLocal, string? EndsLocal);

/// <summary>Configuração atual de uma seção, com quem alterou por último (null = ainda nos valores padrão).</summary>
public sealed record SettingView<T>(T Value, DateTimeOffset? UpdatedUtc, string? UpdatedBy);

public sealed record MediaResult(StaffAuthStatus Status, Guid? Id = null, string? Url = null, IReadOnlyDictionary<string, string>? Errors = null);

public interface ISiteContentStore
{
    Task<SiteSettingRow?> GetSettingAsync(string key, CancellationToken ct);

    Task SaveSettingAsync(string key, string json, string by, CancellationToken ct);

    Task<IReadOnlyList<BannerRecord>> ListBannersAsync(CancellationToken ct);

    Task<BannerRecord?> GetBannerAsync(int id, CancellationToken ct);

    Task<int> CreateBannerAsync(BannerData data, string by, CancellationToken ct);

    Task UpdateBannerAsync(int id, BannerData data, string by, CancellationToken ct);

    Task<bool> DeleteBannerAsync(int id, CancellationToken ct);

    Task SaveMediaAsync(Guid id, CleanAttachment file, string by, CancellationToken ct);

    Task<AttachmentContent?> GetMediaAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Conteúdo institucional do site (texto do servidor, downloads, redes sociais) e banners da home.
/// A equipe edita no painel; o público lê com cache curto. Sem nada salvo, valem os valores do appsettings.
/// </summary>
public sealed partial class SiteContentService(
    ISiteContentStore store,
    IAuditLog audit,
    IMemoryCache cache,
    TimeProvider clock,
    IOptions<ServerPresentationOptions> presentation,
    ILogger<SiteContentService> logger) : IDisposable
{
    public const string ServerKey = "server";
    public const string DownloadsKey = "downloads";
    public const int MaxRates = 12;
    public const int MaxLinks = 8;
    public const int MaxDownloads = 10;
    public const int MaxPublicBanners = 5;
    public const string MediaPrefix = "/midia/";

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private CancellationTokenSource _generation = new();

    public void Dispose() => _generation.Dispose();

    // ---------- Público ----------

    /// <summary>Conteúdo para o site. Banco do portal fora do ar: valores do appsettings, sem banners.</summary>
    public async Task<SiteSnapshot> GetAsync(CancellationToken ct)
    {
        Cached? data;
        try
        {
            data = await cache.GetOrCreateAsync("site:content", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                entry.AddExpirationToken(new CancellationChangeToken(_generation.Token));
                var server = await store.GetSettingAsync(ServerKey, ct).ConfigureAwait(false);
                var downloads = await store.GetSettingAsync(DownloadsKey, ct).ConfigureAwait(false);
                var banners = await store.ListBannersAsync(ct).ConfigureAwait(false);
                return new Cached(
                    ParseServer(server?.Value) ?? DefaultServer(),
                    ParseDownloads(downloads?.Value) ?? EmptyDownloads,
                    banners.Where(b => b.Active).OrderBy(b => b.SortOrder).ThenByDescending(b => b.Id).ToList(),
                    server?.UpdatedUtc,
                    downloads?.UpdatedUtc);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadFailed(logger, ex);
            data = null;
        }

        if (data is null)
        {
            return new SiteSnapshot(DefaultServer(), EmptyDownloads, [], null, null);
        }

        var now = clock.GetUtcNow();
        var visible = data.Banners
            .Where(b => (b.StartsUtc is null || b.StartsUtc <= now) && (b.EndsUtc is null || b.EndsUtc > now))
            .Take(MaxPublicBanners)
            .Select(b => new PublicBanner(b.Id, b.Title, b.Subtitle, b.ImageUrl, b.LinkUrl, b.LinkLabel))
            .ToList();
        return new SiteSnapshot(data.Server, data.Downloads, visible, data.ServerUpdatedUtc, data.DownloadsUpdatedUtc);
    }

    public Task<AttachmentContent?> GetMediaAsync(Guid id, CancellationToken ct) => store.GetMediaAsync(id, ct);

    // ---------- Painel: textos ----------

    public async Task<SettingView<ServerSettings>> GetServerSettingsAsync(CancellationToken ct)
    {
        var row = await store.GetSettingAsync(ServerKey, ct).ConfigureAwait(false);
        return new(ParseServer(row?.Value) ?? DefaultServer(), row?.UpdatedUtc, row?.UpdatedBy);
    }

    public async Task<SettingView<DownloadSettings>> GetDownloadSettingsAsync(CancellationToken ct)
    {
        var row = await store.GetSettingAsync(DownloadsKey, ct).ConfigureAwait(false);
        return new(ParseDownloads(row?.Value) ?? EmptyDownloads, row?.UpdatedUtc, row?.UpdatedBy);
    }

    public async Task<SaveResult> SaveServerSettingsAsync(StaffPrincipal me, ServerSettingsInput input, ClientInfo client, CancellationToken ct)
    {
        var (value, errors) = ValidateServer(input);
        if (value is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        await store.SaveSettingAsync(ServerKey, JsonSerializer.Serialize(value, Json), me.Username, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "site.server.updated", "site", ServerKey, client.IpAddress,
            JsonSerializer.Serialize(new { value.Name, rates = value.Rates.Count, links = value.CommunityLinks.Count, aboutLength = value.About.Length }), ct).ConfigureAwait(false);
        Invalidate();
        return new(StaffAuthStatus.Ok);
    }

    public async Task<SaveResult> SaveDownloadSettingsAsync(StaffPrincipal me, DownloadSettingsInput input, ClientInfo client, CancellationToken ct)
    {
        var (value, errors) = ValidateDownloads(input);
        if (value is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        await store.SaveSettingAsync(DownloadsKey, JsonSerializer.Serialize(value, Json), me.Username, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "site.downloads.updated", "site", DownloadsKey, client.IpAddress,
            JsonSerializer.Serialize(new { files = value.Items.Select(i => i.Name) }), ct).ConfigureAwait(false);
        Invalidate();
        return new(StaffAuthStatus.Ok);
    }

    public static (ServerSettings? Value, Dictionary<string, string> Errors) ValidateServer(ServerSettingsInput input)
    {
        var errors = new Dictionary<string, string>();
        var name = ContentAdminService.Text(input.Name, 40, "name", errors, required: true)!;
        var tagline = ContentAdminService.Text(input.Tagline, 200, "tagline", errors, required: false) ?? string.Empty;
        var about = ContentAdminService.Text(input.About, 20_000, "about", errors, required: false) ?? string.Empty;

        var rates = new List<RateItem>();
        foreach (var (r, i) in (input.Rates ?? []).Select((r, i) => (r, i + 1)))
        {
            var label = r.Label?.Trim() ?? string.Empty;
            var val = r.Value?.Trim() ?? string.Empty;
            if (label.Length == 0 && val.Length == 0)
            {
                continue;
            }

            if (label.Length is 0 or > 30 || val.Length is 0 or > 30)
            {
                errors["rates"] = $"Linha {i}: preencha nome e valor, com até 30 caracteres cada.";
                continue;
            }

            rates.Add(new RateItem(label, val));
        }

        if (rates.Count > MaxRates)
        {
            errors["rates"] = $"Use no máximo {MaxRates} taxas.";
        }

        var links = new List<LinkItem>();
        foreach (var (l, i) in (input.CommunityLinks ?? []).Select((l, i) => (l, i + 1)))
        {
            var label = l.Label?.Trim() ?? string.Empty;
            var url = l.Url?.Trim() ?? string.Empty;
            if (label.Length == 0 && url.Length == 0)
            {
                continue;
            }

            if (label.Length is 0 or > 30)
            {
                errors["communityLinks"] = $"Linha {i}: informe o nome da rede (até 30 caracteres).";
                continue;
            }

            if (!IsHttps(url))
            {
                errors["communityLinks"] = $"Linha {i}: o endereço precisa começar com https://.";
                continue;
            }

            links.Add(new LinkItem(label, url));
        }

        if (links.Count > MaxLinks)
        {
            errors["communityLinks"] = $"Use no máximo {MaxLinks} links.";
        }

        return errors.Count > 0 ? (null, errors) : (new ServerSettings(name, tagline, about, rates, links), errors);
    }

    public static (DownloadSettings? Value, Dictionary<string, string> Errors) ValidateDownloads(DownloadSettingsInput input)
    {
        var errors = new Dictionary<string, string>();
        var intro = ContentAdminService.Text(input.Intro, 4_000, "intro", errors, required: false) ?? string.Empty;
        var requirements = ContentAdminService.Text(input.Requirements, 4_000, "requirements", errors, required: false) ?? string.Empty;

        var items = new List<DownloadItem>();
        foreach (var (d, i) in (input.Items ?? []).Select((d, i) => (d, i + 1)))
        {
            var name = d.Name?.Trim() ?? string.Empty;
            var url = d.Url?.Trim() ?? string.Empty;
            var description = Optional(d.Description);
            var size = Optional(d.Size);
            var version = Optional(d.Version);
            if (name.Length == 0 && url.Length == 0 && description is null && size is null && version is null)
            {
                continue;
            }

            if (name.Length is 0 or > 80)
            {
                errors["items"] = $"Arquivo {i}: informe o nome (até 80 caracteres).";
                continue;
            }

            var linkErrors = new Dictionary<string, string>();
            var link = url.Length == 0 ? null : ContentAdminService.Image(url, linkErrors, "url");
            if (link is null)
            {
                errors["items"] = $"Arquivo {i}: o link precisa começar com https:// (ou / para um arquivo do próprio site).";
                continue;
            }

            if (description?.Length > 300 || size?.Length > 20 || version?.Length > 30)
            {
                errors["items"] = $"Arquivo {i}: descrição até 300, tamanho até 20 e versão até 30 caracteres.";
                continue;
            }

            items.Add(new DownloadItem(name, description, link, size, version));
        }

        if (items.Count > MaxDownloads)
        {
            errors["items"] = $"Use no máximo {MaxDownloads} arquivos.";
        }

        return errors.Count > 0 ? (null, errors) : (new DownloadSettings(intro, requirements, items), errors);
    }

    // ---------- Painel: banners ----------

    public Task<IReadOnlyList<BannerRecord>> ListBannersAsync(CancellationToken ct) => store.ListBannersAsync(ct);

    public Task<BannerRecord?> GetBannerAsync(int id, CancellationToken ct) => store.GetBannerAsync(id, ct);

    public async Task<SaveResult> SaveBannerAsync(StaffPrincipal me, int? id, BannerInput input, ClientInfo client, CancellationToken ct)
    {
        if (id is not null && await store.GetBannerAsync(id.Value, ct).ConfigureAwait(false) is null)
        {
            return new(StaffAuthStatus.NotFound);
        }

        var (data, errors) = ValidateBanner(input);
        if (data is null)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        int savedId;
        if (id is null)
        {
            savedId = await store.CreateBannerAsync(data, me.Username, ct).ConfigureAwait(false);
        }
        else
        {
            await store.UpdateBannerAsync(id.Value, data, me.Username, ct).ConfigureAwait(false);
            savedId = id.Value;
        }

        await audit.WriteAsync("staff", me.Username, id is null ? "banner.created" : "banner.updated", "banner", savedId.ToString(CultureInfo.InvariantCulture),
            client.IpAddress, JsonSerializer.Serialize(new { data.Title, data.Active, data.SortOrder, data.StartsUtc, data.EndsUtc }), ct).ConfigureAwait(false);
        Invalidate();
        return new(StaffAuthStatus.Ok, savedId);
    }

    public async Task<SaveResult> DeleteBannerAsync(StaffPrincipal me, int id, ClientInfo client, CancellationToken ct)
    {
        var before = await store.GetBannerAsync(id, ct).ConfigureAwait(false);
        if (before is null || !await store.DeleteBannerAsync(id, ct).ConfigureAwait(false))
        {
            return new(StaffAuthStatus.NotFound);
        }

        await audit.WriteAsync("staff", me.Username, "banner.deleted", "banner", id.ToString(CultureInfo.InvariantCulture), client.IpAddress,
            JsonSerializer.Serialize(new { before.Title }), ct).ConfigureAwait(false);
        Invalidate();
        return new(StaffAuthStatus.Ok, id);
    }

    public static (BannerData? Value, Dictionary<string, string> Errors) ValidateBanner(BannerInput input)
    {
        var errors = new Dictionary<string, string>();
        var title = ContentAdminService.Text(input.Title, 120, "title", errors, required: true)!;
        var subtitle = ContentAdminService.Text(input.Subtitle, 300, "subtitle", errors, required: false);
        var image = ContentAdminService.Image(input.ImageUrl, errors);
        var link = ContentAdminService.Image(input.LinkUrl, errors, "linkUrl");
        var label = ContentAdminService.Text(input.LinkLabel, 40, "linkLabel", errors, required: false);
        if (link is not null && label is null)
        {
            label = "Saiba mais";
        }

        var order = input.SortOrder ?? 0;
        if (order is < 0 or > 999)
        {
            errors["sortOrder"] = "Use um número de 0 a 999.";
        }

        var starts = ContentAdminService.Local(input.StartsLocal, "startsLocal", errors);
        var ends = ContentAdminService.Local(input.EndsLocal, "endsLocal", errors);
        if (starts is not null && ends is not null && ends <= starts)
        {
            errors["endsLocal"] = "O fim precisa ser depois do início.";
        }

        return errors.Count > 0
            ? (null, errors)
            : (new BannerData(title, subtitle, image, link, link is null ? null : label, order, input.Active, starts, ends), errors);
    }

    // ---------- Painel: imagens ----------

    /// <summary>Recebe uma imagem PNG ou JPG, tira os metadados e devolve o endereço público (/midia/&lt;id&gt;).</summary>
    public async Task<MediaResult> UploadMediaAsync(StaffPrincipal me, UploadedFile? file, ClientInfo client, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Invalid("Escolha uma imagem.");
        }

        if (file.Length > ImageSanitizer.MaxBytes)
        {
            return Invalid("A imagem pode ter no máximo 3 MB.");
        }

        var image = ImageSanitizer.Clean(file.FileName, await file.ReadAsync(ct).ConfigureAwait(false));
        if (image is null || image.ContentType is not ("image/png" or "image/jpeg"))
        {
            return Invalid("Envie uma imagem PNG ou JPG de até 3 MB.");
        }

        var id = Guid.NewGuid();
        await store.SaveMediaAsync(id, image, me.Username, ct).ConfigureAwait(false);
        await audit.WriteAsync("staff", me.Username, "media.uploaded", "media", id.ToString("N"), client.IpAddress,
            JsonSerializer.Serialize(new { image.FileName, image.ContentType, size = image.Data.Length }), ct).ConfigureAwait(false);
        return new(StaffAuthStatus.Ok, id, MediaPrefix + id.ToString("N"));

        static MediaResult Invalid(string message) => new(StaffAuthStatus.ValidationFailed, null, null, new Dictionary<string, string> { ["file"] = message });
    }

    // ---------- Apoio ----------

    public void Invalidate()
    {
        var old = Interlocked.Exchange(ref _generation, new CancellationTokenSource());
        old.Cancel();
    }

    private ServerSettings DefaultServer()
    {
        var p = presentation.Value;
        return new ServerSettings(
            p.Name,
            p.Tagline,
            string.Empty,
            p.Rates.Select(r => new RateItem(r.Key, r.Value)).ToList(),
            p.CommunityLinks.Where(l => IsHttps(l.Url) && l.Label.Length > 0).Select(l => new LinkItem(l.Label, l.Url)).ToList());
    }

    private static readonly DownloadSettings EmptyDownloads = new(string.Empty, string.Empty, []);

    private static ServerSettings? ParseServer(string? json) => Parse<ServerSettings>(json) is { } s
        ? s with { Tagline = s.Tagline ?? string.Empty, About = s.About ?? string.Empty, Rates = s.Rates ?? [], CommunityLinks = s.CommunityLinks ?? [] }
        : null;

    private static DownloadSettings? ParseDownloads(string? json) => Parse<DownloadSettings>(json) is { } d
        ? d with { Intro = d.Intro ?? string.Empty, Requirements = d.Requirements ?? string.Empty, Items = d.Items ?? [] }
        : null;

    private static T? Parse<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsHttps(string? url) =>
        url is { Length: > 0 and <= 400 }
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler o conteúdo do site no banco do portal")]
    private static partial void LogReadFailed(ILogger logger, Exception ex);

    private sealed record Cached(ServerSettings Server, DownloadSettings Downloads, IReadOnlyList<BannerRecord> Banners, DateTimeOffset? ServerUpdatedUtc, DateTimeOffset? DownloadsUpdatedUtc);
}
