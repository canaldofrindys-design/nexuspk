using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Home;
using Priston.Application.Server;

namespace Priston.UnitTests;

public class HomeServiceTests
{
    private sealed class FailingReader : ILevelRankingReader
    {
        public Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sem banco");
    }

    private sealed class UnknownStatus : IServerStatusProvider
    {
        public Task<ServerStatus> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ServerStatus(ServerAvailability.Unknown, null, null));
    }

    [Fact]
    public async Task Database_failure_marks_ranking_unavailable_without_inventing_data()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var home = new HomeService(
            new RankingService(new FailingReader(), cache),
            new UnknownStatus(),
            content.Service(),
            Options.Create(new ServerPresentationOptions()));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.False(snapshot.RankingAvailable);
        Assert.Empty(snapshot.TopPlayers);
        Assert.Empty(snapshot.News);
        Assert.Null(snapshot.Status.PlayersOnline);
    }

    [Fact]
    public async Task Home_lists_published_news_and_live_events()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var content = new ContentFixture();
        var now = ContentFixture.Now;
        content.News.Rows.Add((new Priston.Application.Content.NewsArticle("n1", "noticia", "T", "R", "B", null, null, now.AddHours(-1), now), false));
        content.Events.Rows.Add(ContentFixture.Event(1, "agora", now.AddMinutes(-10)));
        content.Events.Rows.Add(ContentFixture.Event(2, "depois", now.AddDays(1)));
        var home = new HomeService(new RankingService(new FailingReader(), cache), new UnknownStatus(), content.Service(), Options.Create(new ServerPresentationOptions()));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.Equal(["n1"], snapshot.News.Select(n => n.Slug));
        Assert.Equal(["agora"], snapshot.ActiveEvents.Select(e => e.Slug));
        Assert.Equal(["depois"], snapshot.UpcomingEvents.Select(e => e.Slug));
    }
}
