using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Server;

namespace Priston.Application.Home;

public sealed record HomeNewsItem(string Slug, string Title, string Summary, DateTimeOffset PublishedAtUtc);

public sealed record HomeEventItem(string Slug, string Name, string Category, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);

public sealed record HomeServerInfo(string Name, string Tagline, IReadOnlyDictionary<string, string> Rates, IReadOnlyList<CommunityLink> CommunityLinks);

public sealed record HomeSnapshot(
    HomeServerInfo Server,
    ServerStatus Status,
    IReadOnlyList<RankedCharacter> TopPlayers,
    bool RankingAvailable,
    IReadOnlyList<HomeNewsItem> News,
    IReadOnlyList<HomeEventItem> ActiveEvents,
    IReadOnlyList<HomeEventItem> UpcomingEvents);

/// <summary>
/// Monta a homepage só com dados reais. Notícias e eventos ficam vazios até a Fase 8.
/// Falha numa fonte não derruba a página: a seção correspondente vem vazia e marcada.
/// </summary>
public sealed class HomeService(
    RankingService ranking,
    IServerStatusProvider status,
    IOptions<ServerPresentationOptions> presentation)
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

        var p = presentation.Value;
        return new HomeSnapshot(
            new HomeServerInfo(p.Name, p.Tagline, p.Rates, p.CommunityLinks),
            await statusTask.ConfigureAwait(false),
            top,
            rankingAvailable,
            News: [],
            ActiveEvents: [],
            UpcomingEvents: []);
    }
}
