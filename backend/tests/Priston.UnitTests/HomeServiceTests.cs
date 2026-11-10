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
        var home = new HomeService(
            new RankingService(new FailingReader(), cache),
            new UnknownStatus(),
            Options.Create(new ServerPresentationOptions()));

        var snapshot = await home.GetAsync(CancellationToken.None);

        Assert.False(snapshot.RankingAvailable);
        Assert.Empty(snapshot.TopPlayers);
        Assert.Empty(snapshot.News);
        Assert.Null(snapshot.Status.PlayersOnline);
    }
}
