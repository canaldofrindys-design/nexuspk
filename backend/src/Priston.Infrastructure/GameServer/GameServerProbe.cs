using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Server;

namespace Priston.Infrastructure.GameServer;

public sealed class GameServerOptions
{
    public const string SectionName = "GameServer";

    /// <summary>Host do servidor de login/jogo. Vazio = status desconhecido.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public int TimeoutMilliseconds { get; set; } = 2000;
}

/// <summary>
/// Status real: tenta abrir uma conexão TCP na porta do jogo e fecha em seguida.
/// O resultado fica em cache por 30 segundos para não sobrecarregar o servidor.
/// </summary>
internal sealed class GameServerProbe(
    IOptions<GameServerOptions> options,
    IMemoryCache cache,
    TimeProvider clock) : IServerStatusProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<ServerStatus> GetAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host) || o.Port is <= 0 or > 65535)
        {
            return new ServerStatus(ServerAvailability.Unknown, null, null);
        }

        return (await cache.GetOrCreateAsync("server:status", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var online = await CanConnectAsync(o, cancellationToken).ConfigureAwait(false);
            return new ServerStatus(online ? ServerAvailability.Online : ServerAvailability.Offline, clock.GetUtcNow(), null);
        }).ConfigureAwait(false))!;
    }

    private static async Task<bool> CanConnectAsync(GameServerOptions o, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(o.TimeoutMilliseconds);
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(o.Host, o.Port, timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
