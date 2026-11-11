using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Server;

namespace Priston.Infrastructure.GameServer;

public sealed class GameServerOptions
{
    public const string SectionName = "GameServer";

    /// <summary>Host do servidor de login/jogo. Usado quando <see cref="Targets"/> está vazio. Vazio = status desconhecido.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public int TimeoutMilliseconds { get; set; } = 2000;

    /// <summary>Endereços testados: o login e cada canal, com o nome mostrado na página de status.</summary>
    public IList<GameServerTarget> Targets { get; } = [];
}

public sealed class GameServerTarget
{
    /// <summary>Chave curta e estável (ex.: "login", "canal-1"). É a chave do histórico.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }
}

/// <summary>
/// Status real: tenta abrir uma conexão TCP em cada porta configurada e fecha em seguida.
/// O resultado fica em cache por 30 segundos para não sobrecarregar o servidor.
/// </summary>
internal sealed class GameServerProbe(
    IOptions<GameServerOptions> options,
    IMemoryCache cache,
    TimeProvider clock) : IServerProbe
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public IReadOnlyList<ProbeTarget> Targets => Resolve().Select(t => new ProbeTarget(t.Key, t.Name)).ToList();

    public async Task<ServerStatus> GetAsync(CancellationToken cancellationToken)
    {
        var s = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return new ServerStatus(s.Availability, s.CheckedAtUtc, null);
    }

    public async Task<ServerSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        if (Resolve().Count == 0)
        {
            return new ServerSnapshot(ServerAvailability.Unknown, null, []);
        }

        return (await cache.GetOrCreateAsync("server:snapshot", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await ProbeNowAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false))!;
    }

    public async Task<ServerSnapshot> ProbeNowAsync(CancellationToken ct)
    {
        var targets = Resolve();
        if (targets.Count == 0)
        {
            return new ServerSnapshot(ServerAvailability.Unknown, null, []);
        }

        var timeout = options.Value.TimeoutMilliseconds;
        var results = await Task.WhenAll(targets.Select(async t =>
        {
            var ms = await ConnectAsync(t.Host, t.Port, timeout, ct).ConfigureAwait(false);
            return new TargetStatus(t.Key, t.Name, ms is not null, ms);
        })).ConfigureAwait(false);
        return new ServerSnapshot(IServerProbe.Combine(results), clock.GetUtcNow(), results);
    }

    private List<GameServerTarget> Resolve()
    {
        var o = options.Value;
        var list = o.Targets
            .Where(t => !string.IsNullOrWhiteSpace(t.Key) && !string.IsNullOrWhiteSpace(t.Host) && t.Port is > 0 and <= 65535)
            .Select(t => new GameServerTarget { Key = t.Key.Trim().ToLowerInvariant(), Name = string.IsNullOrWhiteSpace(t.Name) ? t.Key : t.Name.Trim(), Host = t.Host, Port = t.Port })
            .ToList();
        if (list.Count == 0 && !string.IsNullOrWhiteSpace(o.Host) && o.Port is > 0 and <= 65535)
        {
            list.Add(new GameServerTarget { Key = "servidor", Name = "Servidor", Host = o.Host, Port = o.Port });
        }

        return list;
    }

    /// <summary>Tempo de conexão em ms, ou null se a porta não respondeu.</summary>
    private static async Task<int?> ConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        using var client = new TcpClient();
        var watch = Stopwatch.StartNew();
        try
        {
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return (int)Math.Min(int.MaxValue, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
