using System.Net;
using System.Net.NetworkInformation;
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

    private sealed class FakeCounter(int? count) : IOnlinePlayersCounter
    {
        public List<(int Port, bool ByAddress)> Calls { get; } = [];

        public int? Count(int port, bool byAddress)
        {
            Calls.Add((port, byAddress));
            return count;
        }
    }

    private static GameServerProbe CreateWith(int port, MemoryCache cache, IOnlinePlayersCounter counter, bool enabled, string countBy = "Connections") =>
        new(CreateOptions(port, enabled, countBy), cache, TimeProvider.System, counter);

    private static IOptions<GameServerOptions> CreateOptions(int port, bool enabled, string countBy)
    {
        var o = new GameServerOptions { TimeoutMilliseconds = 1000 };
        o.Targets.Add(new GameServerTarget { Key = "servidor", Name = "Servidor", Host = "127.0.0.1", Port = port });
        o.OnlinePlayers.Enabled = enabled;
        o.OnlinePlayers.CountBy = countBy;
        return Options.Create(o);
    }

    [Fact]
    public async Task Players_counted_on_the_target_port_when_enabled_and_cached()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var counter = new FakeCounter(4);
            var probe = CreateWith(port, cache, counter, enabled: true, countBy: "addresses");
            Assert.Equal(4, (await probe.GetAsync(CancellationToken.None)).PlayersOnline);
            Assert.Equal(4, (await probe.GetAsync(CancellationToken.None)).PlayersOnline);
            Assert.Equal([(port, true)], counter.Calls);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task No_players_number_when_disabled_or_server_down()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var off = new FakeCounter(4);
            Assert.Null((await CreateWith(port, cache, off, enabled: false).GetAsync(CancellationToken.None)).PlayersOnline);
            Assert.Empty(off.Calls);
        }
        finally
        {
            listener.Stop();
        }

        using var cache2 = new MemoryCache(new MemoryCacheOptions());
        var down = new FakeCounter(4);
        Assert.Null((await CreateWith(port, cache2, down, enabled: true).GetAsync(CancellationToken.None)).PlayersOnline);
        Assert.Empty(down.Calls);
    }

    private sealed class FakeConnection(string local, string remote, TcpState state) : TcpConnectionInformation
    {
        public override IPEndPoint LocalEndPoint { get; } = IPEndPoint.Parse(local);

        public override IPEndPoint RemoteEndPoint { get; } = IPEndPoint.Parse(remote);

        public override TcpState State { get; } = state;
    }

    [Fact]
    public void Counter_counts_established_remote_connections_on_the_port()
    {
        var list = new TcpConnectionInformation[]
        {
            new FakeConnection("10.0.0.5:35002", "200.1.1.1:50001", TcpState.Established),
            new FakeConnection("10.0.0.5:35002", "200.1.1.1:50002", TcpState.Established),
            new FakeConnection("[::ffff:10.0.0.5]:35002", "[::ffff:200.1.1.1]:50003", TcpState.Established),
            new FakeConnection("10.0.0.5:35002", "189.2.2.2:40000", TcpState.Established),
            new FakeConnection("10.0.0.5:35002", "177.3.3.3:40000", TcpState.TimeWait),
            new FakeConnection("127.0.0.1:35002", "127.0.0.1:61000", TcpState.Established),
            new FakeConnection("10.0.0.5:32009", "200.1.1.1:50004", TcpState.Established),
            new FakeConnection("10.0.0.5:50005", "200.1.1.1:35002", TcpState.Established),
        };
        var counter = new TcpConnectionCounter(() => list);
        Assert.Equal(4, counter.Count(35002, byAddress: false));
        Assert.Equal(2, counter.Count(35002, byAddress: true));
        Assert.Equal(0, counter.Count(35003, byAddress: false));
    }

    [Fact]
    public void Counter_returns_null_when_the_table_cannot_be_read()
    {
        var counter = new TcpConnectionCounter(() => throw new NetworkInformationException(5));
        Assert.Null(counter.Count(35002, byAddress: false));
    }
}
