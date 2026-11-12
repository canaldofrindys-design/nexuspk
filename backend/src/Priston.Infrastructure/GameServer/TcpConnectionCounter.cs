using System.Net;
using System.Net.NetworkInformation;

namespace Priston.Infrastructure.GameServer;

internal interface IOnlinePlayersCounter
{
    /// <summary>Jogadores conectados na porta local, ou null se a lista de conexões não pôde ser lida.</summary>
    int? Count(int port, bool byAddress);
}

/// <summary>
/// Conta as conexões TCP estabelecidas na porta do servidor do jogo, como o <c>Get-NetTCPConnection</c> mostra.
/// Ignora conexões vindas da própria máquina (o teste de porta do portal e ferramentas locais).
/// Não lê nada do conteúdo das conexões, só a tabela de conexões do Windows.
/// </summary>
internal sealed class TcpConnectionCounter(Func<IEnumerable<TcpConnectionInformation>>? source = null) : IOnlinePlayersCounter
{
    private readonly Func<IEnumerable<TcpConnectionInformation>> source = source ?? (() => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections());

    public int? Count(int port, bool byAddress)
    {
        IEnumerable<TcpConnectionInformation> all;
        try
        {
            all = source();
        }
        catch (NetworkInformationException)
        {
            return null;
        }

        var remote = all
            .Where(c => c.State == TcpState.Established && c.LocalEndPoint.Port == port && !IPAddress.IsLoopback(c.RemoteEndPoint.Address))
            .Select(c => c.RemoteEndPoint);
        return byAddress ? remote.Select(e => Normalize(e.Address)).Distinct().Count() : remote.Count();
    }

    private static IPAddress Normalize(IPAddress a) => a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a;
}
