using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Content;
using Priston.Application.Server;
using Priston.Application.Site;

namespace Priston.Application.Home;

public sealed record HomeNewsItem(string Slug, string Category, string Title, string Summary, DateTimeOffset PublishedAtUtc);

public sealed record HomeEventItem(string Slug, string Name, string Category, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);

/// <summary>Clã em destaque na home (dono do Bless Castle ou líder do SoD). Sem o líder do clã, que pode ser uma conta.</summary>
public sealed record HomeClan(string Name, int Members, int Points);

/// <summary>
/// <paramref name="Available"/> false: a fonte falhou (banco ou arquivo do jogo). Com a fonte no ar e
/// <paramref name="Clan"/> null, não há líder (castelo sem dono, ninguém pontuou no SoD).
/// </summary>
public sealed record HomeClanLeader(bool Available, HomeClan? Clan);

public sealed record HomeServerInfo(string Name, string Tagline, IReadOnlyDictionary<string, string> Rates, IReadOnlyList<LinkItem> CommunityLinks);

public sealed record HomeSnapshot(
    HomeServerInfo Server,
    ServerStatus Status,
    IReadOnlyList<RankedCharacter> TopPlayers,
    bool RankingAvailable,
    IReadOnlyList<HomeNewsItem> News,
    IReadOnlyList<HomeEventItem> ActiveEvents,
    IReadOnlyList<HomeEventItem> UpcomingEvents,
    IReadOnlyList<PublicBanner> Banners,
    IReadOnlyList<PvpEntry>? TopBattlePoints = null,
    bool BattlePointsAvailable = false,
    HomeClanLeader? BlessCastle = null,
    HomeClanLeader? SodLeader = null);

/// <summary>
/// Monta a homepage só com dados reais (ranking, status, notícias e eventos publicados).
/// Falha numa fonte não derruba a página: a seção correspondente vem vazia e marcada.
/// </summary>
public sealed class HomeService(
    RankingService ranking,
    IServerStatusProvider status,
    ContentService content,
    IOptions<ServerPresentationOptions> presentation,
    SiteContentService? site = null,
    RankingQueryService? rankings = null,
    IBlessCastleOwnerReader? blessCastle = null)
{
    /// <summary>Quantos personagens o ranking de BP da home mostra.</summary>
    public const int BattlePointsTop = 5;

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

        var (battlePoints, battlePointsAvailable) = await TopBattlePointsAsync(cancellationToken).ConfigureAwait(false);
        var (castle, sod) = await ClanLeadersAsync(cancellationToken).ConfigureAwait(false);

        return new HomeSnapshot(
            server,
            await statusTask.ConfigureAwait(false),
            top,
            rankingAvailable,
            news,
            live,
            upcoming,
            banners,
            battlePoints,
            battlePointsAvailable,
            castle,
            sod);
    }

    /// <summary>Os primeiros do ranking de BP (UserDB.dbo.BattlePoints.BPs), sem GMs.</summary>
    private async Task<(IReadOnlyList<PvpEntry> Rows, bool Available)> TopBattlePointsAsync(CancellationToken ct)
    {
        if (rankings is null)
        {
            return ([], false);
        }

        try
        {
            var page = await rankings.GetPvpAsync(PvpKind.BattlePoints, null, 1, BattlePointsTop, ct).ConfigureAwait(false);
            return (page.Items, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ([], false);
        }
    }

    /// <summary>
    /// Dono do Bless Castle (BlessCastle.dat → CL.MIconCnt) e líder do SoD (clã com mais pontos em CL.Cpoint,
    /// a mesma regra do site antigo).
    /// </summary>
    private async Task<(HomeClanLeader Castle, HomeClanLeader Sod)> ClanLeadersAsync(CancellationToken ct)
    {
        var unavailable = new HomeClanLeader(false, null);
        if (rankings is null)
        {
            return (unavailable, unavailable);
        }

        IReadOnlyList<ClanRow> clans;
        try
        {
            clans = await rankings.GetAllClansAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (unavailable, unavailable);
        }

        var sodTop = clans
            .Where(c => c.Points > 0)
            .OrderByDescending(c => c.Points)
            .ThenByDescending(c => c.Members)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var sod = new HomeClanLeader(true, sodTop is null ? null : ToHome(sodTop));

        var castle = unavailable;
        if (blessCastle is not null)
        {
            try
            {
                var owner = await blessCastle.ReadOwnerIconIdAsync(ct).ConfigureAwait(false);
                var clan = owner is null ? null : clans.FirstOrDefault(c => c.IconId == owner.Value);
                castle = new HomeClanLeader(true, clan is null ? null : ToHome(clan));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Arquivo do jogo fora de alcance: o bloco aparece como indisponível.
            }
        }

        return (castle, sod);
    }

    private static HomeClan ToHome(ClanRow c) => new(c.Name, c.Members, c.Points);

    private static HomeEventItem ToHome(EventSummary e) => new(e.Slug, e.Name, e.Category, e.StartsUtc, e.EndsUtc);
}
