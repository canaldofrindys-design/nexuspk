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
}
