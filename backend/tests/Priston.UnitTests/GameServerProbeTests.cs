using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Server;
using Priston.Infrastructure.GameServer;

namespace Priston.UnitTests;

public class GameServerProbeTests
{
    private static GameServerProbe Create(string host, int port, MemoryCache cache) =>
        new(Options.Create(new GameServerOptions { Host = host, Port = port, TimeoutMilliseconds = 1000 }), cache, TimeProvider.System);

    [Fact]
    public async Task Unknown_when_not_configured()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var status = await Create("", 0, cache).GetAsync(CancellationToken.None);
        Assert.Equal(ServerAvailability.Unknown, status.Availability);
        Assert.Null(status.PlayersOnline);
    }

    [Fact]
    public async Task Online_when_port_accepts_connections()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var status = await Create("127.0.0.1", port, cache).GetAsync(CancellationToken.None);
            Assert.Equal(ServerAvailability.Online, status.Availability);
            Assert.NotNull(status.CheckedAtUtc);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Offline_when_port_is_closed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var status = await Create("127.0.0.1", port, cache).GetAsync(CancellationToken.None);
        Assert.Equal(ServerAvailability.Offline, status.Availability);
    }

    private sealed class FakePlayers(int count, bool fail = false) : IOnlinePlayersReader
    {
        public int Calls { get; private set; }

        public Task<int> CountAsync(CancellationToken ct)
        {
            Calls++;
            return fail ? throw new InvalidOperationException("banco fora") : Task.FromResult(count);
        }
    }

    private static GameServerProbe CreateWith(int port, MemoryCache cache, IOnlinePlayersReader players) =>
        new(Options.Create(new GameServerOptions { Host = "127.0.0.1", Port = port, TimeoutMilliseconds = 1000 }), cache, TimeProvider.System, players);

    [Fact]
    public async Task Players_online_counted_only_with_server_up_and_cached()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var players = new FakePlayers(7);
            var probe = CreateWith(port, cache, players);
            Assert.Equal(7, (await probe.GetAsync(CancellationToken.None)).PlayersOnline);
            Assert.Equal(7, (await probe.GetAsync(CancellationToken.None)).PlayersOnline);
            Assert.Equal(1, players.Calls);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task No_players_number_when_server_is_down_or_database_fails()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            Assert.Null((await CreateWith(port, cache, new FakePlayers(3, fail: true)).GetAsync(CancellationToken.None)).PlayersOnline);
        }
        finally
        {
            listener.Stop();
        }

        using var cache2 = new MemoryCache(new MemoryCacheOptions());
        var stale = new FakePlayers(5);
        Assert.Null((await CreateWith(port, cache2, stale).GetAsync(CancellationToken.None)).PlayersOnline);
        Assert.Equal(0, stale.Calls);
    }
}
