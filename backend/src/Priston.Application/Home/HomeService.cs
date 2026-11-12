using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Content;
using Priston.Application.Server;
using Priston.Application.Site;

namespace Priston.Application.Home;

public sealed record HomeNewsItem(string Slug, string Category, string Title, string Summary, DateTimeOffset PublishedAtUtc);

public sealed record HomeEventItem(string Slug, string Name, string Category, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);

public sealed record HomeServerInfo(string Name, string Tagline, IReadOnlyDictionary<string, string> Rates, IReadOnlyList<LinkItem> CommunityLinks);

public sealed record HomeSnapshot(
    HomeServerInfo Server,
    ServerStatus Status,
    IReadOnlyList<RankedCharacter> TopPlayers,
    bool RankingAvailable,
    IReadOnlyList<HomeNewsItem> News,
    IReadOnlyList<HomeEventItem> ActiveEvents,
    IReadOnlyList<HomeEventItem> UpcomingEvents,
    IReadOnlyList<PublicBanner> Banners);

/// <summary>
/// Monta a homepage só com dados reais (ranking, status, notícias e eventos publicados).
/// Falha numa fonte não derruba a página: a seção correspondente vem vazia e marcada.
/// </summary>
public sealed class HomeService(
    RankingService ranking,
    IServerStatusProvider status,
    ContentService content,
    IOptions<ServerPresentationOptions> presentation,
    SiteContentService? site = null)
{
    public async Task<HomeSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var statusTask = status.GetAsync(cancellationToken);

        IReadOnlyList<RankedCharacter> top = [];
        var rankingAvailable = true;
        try
        {
            top = await ranking.GetTopAsync(5, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            rankingAvailable = false;
        }

        IReadOnlyList<HomeNewsItem> news = [];
        List<HomeEventItem> live = [];
        IReadOnlyList<HomeEventItem> upcoming = [];
        try
        {
            var page = await content.ListNewsAsync(null, 1, 5, cancellationToken).ConfigureAwait(false);
            news = page.Items.Select(n => new HomeNewsItem(n.Slug, n.Category, n.Title, n.Summary, n.PublishedUtc)).ToList();
            var overview = await content.GetOverviewAsync(cancellationToken).ConfigureAwait(false);
            live = overview.Live.Select(ToHome).ToList();
            upcoming = overview.Upcoming.Where(e => e.State != EventState.Cancelled).Take(Math.Max(0, 6 - live.Count)).Select(ToHome).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Banco do portal indisponível: a home continua com as outras seções.
        }

        // Textos editados no painel; sem eles (ou sem o banco do portal), os do appsettings.
        HomeServerInfo server;
        IReadOnlyList<PublicBanner> banners = [];
        if (site is not null)
        {
            var snapshot = await site.GetAsync(cancellationToken).ConfigureAwait(false);
            var rates = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in snapshot.Server.Rates)
            {
                rates.TryAdd(r.Label, r.Value);
            }

            server = new HomeServerInfo(snapshot.Server.Name, snapshot.Server.Tagline, rates, snapshot.Server.CommunityLinks);
            banners = snapshot.Banners;
        }
        else
        {
            var p = presentation.Value;
            server = new HomeServerInfo(p.Name, p.Tagline, p.Rates, p.CommunityLinks.Select(l => new LinkItem(l.Label, l.Url)).ToList());
        }

        return new HomeSnapshot(
            server,
            await statusTask.ConfigureAwait(false),
            top,
            rankingAvailable,
            news,
            live,
            upcoming,
            banners);
    }

    private static HomeEventItem ToHome(EventSummary e) => new(e.Slug, e.Name, e.Category, e.StartsUtc, e.EndsUtc);
}
