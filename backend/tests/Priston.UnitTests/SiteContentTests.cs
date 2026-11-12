using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Home;
using Priston.Application.Site;
using Priston.Application.Support;

namespace Priston.UnitTests;

internal sealed class MemorySite : ISiteContentStore
{
    public Dictionary<string, SiteSettingRow> Settings { get; } = [];
    public Dictionary<int, BannerData> Banners { get; } = [];
    public Dictionary<Guid, CleanAttachment> Media { get; } = [];
    public bool Fail { get; set; }

    public Task<SiteSettingRow?> GetSettingAsync(string key, CancellationToken ct) =>
        Fail ? throw new InvalidOperationException("sem banco") : Task.FromResult(Settings.GetValueOrDefault(key));

    public Task SaveSettingAsync(string key, string json, string by, CancellationToken ct)
    {
        Settings[key] = new SiteSettingRow(json, ContentFixture.Now, by);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BannerRecord>> ListBannersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BannerRecord>>(Banners.Select(b => ToRecord(b.Key, b.Value)).ToList());

    public Task<BannerRecord?> GetBannerAsync(int id, CancellationToken ct) =>
        Task.FromResult(Banners.TryGetValue(id, out var d) ? ToRecord(id, d) : null);

    public Task<int> CreateBannerAsync(BannerData data, string by, CancellationToken ct)
    {
        var id = Banners.Count == 0 ? 1 : Banners.Keys.Max() + 1;
        Banners[id] = data;
        return Task.FromResult(id);
    }

    public Task UpdateBannerAsync(int id, BannerData data, string by, CancellationToken ct)
    {
        Banners[id] = data;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteBannerAsync(int id, CancellationToken ct) => Task.FromResult(Banners.Remove(id));

    public Task SaveMediaAsync(Guid id, CleanAttachment file, string by, CancellationToken ct)
    {
        Media[id] = file;
        return Task.CompletedTask;
    }

    public Task<AttachmentContent?> GetMediaAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Media.TryGetValue(id, out var f) ? new AttachmentContent(f.FileName, f.ContentType, f.Data) : null);

    private static BannerRecord ToRecord(int id, BannerData d) => new(id, d.Title, d.Subtitle, d.ImageUrl, d.LinkUrl, d.LinkLabel, d.SortOrder, d.Active,
        d.StartsUtc, d.EndsUtc, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null);
}

public sealed class SiteContentTests : IDisposable
{
    private static readonly DateTimeOffset Now = ContentFixture.Now;
    private static readonly ClientInfo Client = new("198.51.100.7", "teste");
    private static readonly StaffPrincipal Admin = new(1, "ana", "Ana", StaffRoles.Admin, false, Now.AddHours(1));
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly MemorySite _store = new();
    private readonly MemoryAudit _audit = new();

    public void Dispose() => _cache.Dispose();

    private SiteContentService Service(ServerPresentationOptions? presentation = null) => new(
        _store,
        _audit,
        _cache,
        new FixedClock(Now),
        Options.Create(presentation ?? new ServerPresentationOptions { Name = "NexusPK", Rates = new() { ["EXP"] = "10x" } }),
        NullLogger<SiteContentService>.Instance);

    [Fact]
    public async Task Without_saved_texts_the_site_uses_appsettings_and_survives_database_failure()
    {
        using var svc = Service();
        var site = await svc.GetAsync(CancellationToken.None);
        Assert.Equal("NexusPK", site.Server.Name);
        Assert.Equal([new RateItem("EXP", "10x")], site.Server.Rates);
        Assert.Empty(site.Downloads.Items);

        _store.Fail = true;
        using var failing = new SiteContentService(_store, _audit, new MemoryCache(new MemoryCacheOptions()), new FixedClock(Now),
            Options.Create(new ServerPresentationOptions { Name = "NexusPK" }), NullLogger<SiteContentService>.Instance);
        var fallback = await failing.GetAsync(CancellationToken.None);
        Assert.Equal("NexusPK", fallback.Server.Name);
        Assert.Empty(fallback.Banners);
    }

    [Fact]
    public async Task Server_texts_are_validated_saved_and_shown_right_away()
    {
        using var svc = Service();
        _ = await svc.GetAsync(CancellationToken.None); // aquece o cache

        var bad = await svc.SaveServerSettingsAsync(Admin, new ServerSettingsInput("", null, null,
            [new RateInput("EXP", "")], [new LinkInput("Discord", "javascript:alert(1)")]), Client, CancellationToken.None);
        Assert.Equal(["communityLinks", "name", "rates"], bad.Errors!.Keys.Order());

        var ok = await svc.SaveServerSettingsAsync(Admin, new ServerSettingsInput("Nexus PK", "PvP raiz", "## História\nTexto",
            [new RateInput(" EXP ", "20x"), new RateInput("", "")], [new LinkInput("Discord", "https://discord.gg/nexus"), new LinkInput(null, null)]), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);

        var site = await svc.GetAsync(CancellationToken.None);
        Assert.Equal("Nexus PK", site.Server.Name);
        Assert.Equal([new RateItem("EXP", "20x")], site.Server.Rates);
        Assert.Equal([new LinkItem("Discord", "https://discord.gg/nexus")], site.Server.CommunityLinks);
        Assert.Equal(Now, site.ServerUpdatedUtc);
        Assert.Contains(_audit.Rows, r => r.Action == "site.server.updated" && r.Actor == "ana");
    }

    [Fact]
    public async Task Download_links_must_be_https_or_from_the_site()
    {
        using var svc = Service();
        var bad = await svc.SaveDownloadSettingsAsync(Admin, new DownloadSettingsInput("Baixe", null,
            [new DownloadItemInput("Cliente", null, "http://inseguro.test/cliente.zip", null, null)]), Client, CancellationToken.None);
        Assert.Equal("items", Assert.Single(bad.Errors!).Key);

        var ok = await svc.SaveDownloadSettingsAsync(Admin, new DownloadSettingsInput("Baixe", "Windows 10",
            [new DownloadItemInput("Cliente completo", "Instalador", "https://cdn.example.test/cliente.exe", "1,2 GB", "v1.0"), new DownloadItemInput("", "", "", "", "")]),
            Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);
        var site = await svc.GetAsync(CancellationToken.None);
        var item = Assert.Single(site.Downloads.Items);
        Assert.Equal("https://cdn.example.test/cliente.exe", item.Url);
        Assert.Equal("Windows 10", site.Downloads.Requirements);
    }

    [Fact]
    public async Task Only_active_banners_inside_their_period_reach_the_home_in_order()
    {
        using var svc = Service();
        await svc.SaveBannerAsync(Admin, null, new BannerInput("Segundo", null, null, null, null, 2, true, null, null), Client, CancellationToken.None);
        await svc.SaveBannerAsync(Admin, null, new BannerInput("Primeiro", "Sub", "/midia/abc", "/eventos", null, 1, true, null, null), Client, CancellationToken.None);
        await svc.SaveBannerAsync(Admin, null, new BannerInput("Inativo", null, null, null, null, 0, false, null, null), Client, CancellationToken.None);
        await svc.SaveBannerAsync(Admin, null, new BannerInput("Futuro", null, null, null, null, 0, true, "2026-12-01T00:00", null), Client, CancellationToken.None);
        await svc.SaveBannerAsync(Admin, null, new BannerInput("Vencido", null, null, null, null, 0, true, null, "2026-11-01T00:00"), Client, CancellationToken.None);

        var site = await svc.GetAsync(CancellationToken.None);
        Assert.Equal(["Primeiro", "Segundo"], site.Banners.Select(b => b.Title));
        Assert.Equal("Saiba mais", site.Banners[0].LinkLabel);

        var bad = await svc.SaveBannerAsync(Admin, null, new BannerInput("", null, "data:image/png;base64,xx", "javascript:x", null, 5000, true, null, null), Client, CancellationToken.None);
        Assert.Equal(["imageUrl", "linkUrl", "sortOrder", "title"], bad.Errors!.Keys.Order());

        Assert.Equal(StaffAuthStatus.Ok, (await svc.DeleteBannerAsync(Admin, 1, Client, CancellationToken.None)).Status);
        Assert.Equal(["Primeiro"], (await svc.GetAsync(CancellationToken.None)).Banners.Select(b => b.Title));
    }

    [Fact]
    public async Task Uploaded_media_must_be_a_real_png_or_jpg()
    {
        using var svc = Service();
        var fake = new UploadedFile("virus.png", 4, _ => Task.FromResult("MZ\0\0"u8.ToArray()));
        var bad = await svc.UploadMediaAsync(Admin, fake, Client, CancellationToken.None);
        Assert.Equal("file", Assert.Single(bad.Errors!).Key);

        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var ok = await svc.UploadMediaAsync(Admin, new UploadedFile("banner.png", png.Length, _ => Task.FromResult(png)), Client, CancellationToken.None);
        Assert.Equal(StaffAuthStatus.Ok, ok.Status);
        Assert.Equal("/midia/" + ok.Id!.Value.ToString("N"), ok.Url);
        Assert.Equal("image/png", (await svc.GetMediaAsync(ok.Id.Value, CancellationToken.None))!.ContentType);
    }
}
