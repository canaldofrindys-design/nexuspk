using Priston.Domain.Characters;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Content;
using Priston.Application.Server;
using Priston.Application.Site;

namespace Priston.Application.Home;

public sealed record HomeNewsItem(string Slug, string Category, string Title, string Summary, DateTimeOffset PublishedAtUtc);

public sealed record HomeEventItem(string Slug, string Name, string Category, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc);

/// <summary>Personagem em destaque na home. <paramref name="Level"/> null: o personagem não está na LevelTable.</summary>
public sealed record HomeCharacter(string Name, int ClassCode, string ClassName, int? Level);

/// <summary>
/// Clã em destaque na home (dono do Bless Castle ou líder do SoD).
/// <paramref name="IconId"/>: número do emblema (CL.MIconCnt, imagem em /ClanContent/&lt;número&gt;.bmp).
/// <paramref name="Leader"/>: personagem líder do clã (CL.ClanZang).
/// </summary>
public sealed record HomeClan(
    string Name,
    int Members,
    int Points,
    int Wins = 0,
    int Losses = 0,
    DateTimeOffset? CreatedUtc = null,
    int IconId = 0,
    HomeCharacter? Leader = null,
    int? SodPosition = null);

/// <summary>Melhor personagem do SoD no mês.</summary>
public sealed record HomeSodPlayer(string Name, int ClassCode, string ClassName, int Level, int Points, string? Clan);

/// <summary>
/// <paramref name="Available"/> false: a fonte falhou (banco ou arquivo do jogo). Com a fonte no ar e
/// <paramref name="Clan"/> null, não há líder (castelo sem dono, ninguém pontuou no SoD).
/// <paramref name="TopPlayer"/>: só no SoD, o personagem com mais pontos no mês (null sem tabela do mês ou sem acesso).
/// </summary>
public sealed record HomeClanLeader(bool Available, HomeClan? Clan, HomeSodPlayer? TopPlayer = null);

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
    HomeClanLeader? SodLeader = null,
    PvpTierLadder? PvpTiers = null,
    HomeClanLeader? BiggestClan = null,
    IReadOnlyList<RareDrop>? RareDrops = null,
    IReadOnlyList<ItemShowcase>? Showcase = null,
    IReadOnlyList<ScheduledEvent>? Schedule = null);

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
    IBlessCastleOwnerReader? blessCastle = null,
    ISodRankingReader? sodPlayers = null,
    TimeProvider? clock = null,
    RareDropFeed? dropFeed = null)
{
    private static readonly TimeZoneInfo GameTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Priston.Domain.Time.RankingPeriods.TimeZoneId);

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
        IReadOnlyList<RareDrop> drops = [];
        IReadOnlyList<ItemShowcase> showcase = [];
        var schedule = SiteContentService.DefaultSchedule.Items;
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
            schedule = snapshot.Schedule ?? schedule;
            drops = dropFeed is null ? snapshot.RareDrops ?? [] : await dropFeed.GetAsync(snapshot, cancellationToken).ConfigureAwait(false);
            if (dropFeed is not null && drops.Count < RareDropFeed.MaxShown)
            {
                showcase = await dropFeed.GetShowcaseAsync(RareDropFeed.MaxShown - drops.Count, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var p = presentation.Value;
            server = new HomeServerInfo(p.Name, p.Tagline, p.Rates, p.CommunityLinks.Select(l => new LinkItem(l.Label, l.Url)).ToList());
        }

        var (battlePoints, battlePointsAvailable) = await TopBattlePointsAsync(cancellationToken).ConfigureAwait(false);
        var (castle, sod, biggest) = await ClanLeadersAsync(cancellationToken).ConfigureAwait(false);
        var tiers = await PvpTiersAsync(cancellationToken).ConfigureAwait(false);

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
            sod,
            tiers,
            biggest,
            drops,
            showcase,
            schedule);
    }

    /// <summary>Patentes PvP do jogo (pontos PvP e Rei PvP). Fonte fora: null.</summary>
    private async Task<PvpTierLadder?> PvpTiersAsync(CancellationToken ct)
    {
        if (rankings is null)
        {
            return null;
        }

        try
        {
            return await rankings.GetPvpTiersAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
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
    /// a mesma regra do site antigo) e o maior clã (mais membros).
    /// </summary>
    private async Task<(HomeClanLeader Castle, HomeClanLeader Sod, HomeClanLeader Biggest)> ClanLeadersAsync(CancellationToken ct)
    {
        var unavailable = new HomeClanLeader(false, null);
        if (rankings is null)
        {
            return (unavailable, unavailable, unavailable);
        }

        IReadOnlyList<ClanRow> clans;
        try
        {
            clans = await rankings.GetAllClansAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (unavailable, unavailable, unavailable);
        }

        // Classe e nível do líder vêm da LevelTable (mesmo cache do ranking). Sem ela, só o nome.
        IReadOnlyList<RankedCharacter> characters = [];
        try
        {
            characters = await ranking.GetLevelRankingAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }

        var byName = characters
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var sodOrder = clans
            .Where(c => c.Points > 0)
            .OrderByDescending(c => c.Points)
            .ThenByDescending(c => c.Members)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        HomeClan Detail(ClanRow c)
        {
            var position = sodOrder.IndexOf(c);
            return new HomeClan(
                c.Name, c.Members, c.Points, c.Wins, c.Losses, ToUtc(c.CreatedLocal), c.IconId,
                Character(c.Leader, byName), position < 0 ? null : position + 1);
        }

        var sodTop = sodOrder.FirstOrDefault();
        var sod = new HomeClanLeader(true, sodTop is null ? null : Detail(sodTop), await TopSodPlayerAsync(ct).ConfigureAwait(false));

        var biggestRow = clans
            .Where(c => c.Members > 0)
            .OrderByDescending(c => c.Members)
            .ThenByDescending(c => c.Points)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var biggest = new HomeClanLeader(true, biggestRow is null ? null : Detail(biggestRow));

        var castle = unavailable;
        if (blessCastle is not null)
        {
            try
            {
                var owner = await blessCastle.ReadOwnerIconIdAsync(ct).ConfigureAwait(false);
                var clan = owner is null ? null : clans.FirstOrDefault(c => c.IconId == owner.Value);
                castle = new HomeClanLeader(true, clan is null ? null : Detail(clan));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Arquivo do jogo fora de alcance: o bloco aparece como indisponível.
            }
        }

        return (castle, sod, biggest);
    }

    /// <summary>Melhor personagem do SoD no mês corrente (Brasília). Falha ou sem acesso ao sodDB: null.</summary>
    private async Task<HomeSodPlayer?> TopSodPlayerAsync(CancellationToken ct)
    {
        if (sodPlayers is null || rankings is null)
        {
            return null;
        }

        try
        {
            var local = TimeZoneInfo.ConvertTime((clock ?? TimeProvider.System).GetUtcNow(), GameTimeZone);
            var row = await sodPlayers.ReadTopAsync(new DateOnly(local.Year, local.Month, 1), ct).ConfigureAwait(false);
            if (row is null)
            {
                return null;
            }

            var clans = await rankings.GetClanByCharacterAsync(ct).ConfigureAwait(false);
            var cls = CharacterClassNames.ToPortuguese(CharacterClassNames.FromCode(row.ClassCode));
            return new HomeSodPlayer(row.Name, row.ClassCode, cls, row.Level, row.Points, clans.GetValueOrDefault(row.Name));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static HomeCharacter? Character(string? name, Dictionary<string, RankedCharacter> byName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var n = name.Trim();
        return byName.TryGetValue(n, out var c)
            ? new HomeCharacter(c.Name, c.ClassCode, c.ClassName, c.Level)
            : new HomeCharacter(n, 0, string.Empty, null);
    }

    private static DateTimeOffset? ToUtc(DateTime? local) =>
        local is null ? null : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified), GameTimeZone), TimeSpan.Zero);

    private static HomeEventItem ToHome(EventSummary e) => new(e.Slug, e.Name, e.Category, e.StartsUtc, e.EndsUtc);
}
