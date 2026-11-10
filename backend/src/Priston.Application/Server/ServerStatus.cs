namespace Priston.Application.Server;

public enum ServerAvailability
{
    /// <summary>Sem host/porta configurados: o portal não afirma nada.</summary>
    Unknown,
    Online,
    Offline,
}

/// <summary>
/// <paramref name="PlayersOnline"/> é nulo enquanto não houver fonte real
/// (DATABASE_MAPPING.md, seção 8).
/// </summary>
public sealed record ServerStatus(ServerAvailability Availability, DateTimeOffset? CheckedAtUtc, int? PlayersOnline);

public interface IServerStatusProvider
{
    Task<ServerStatus> GetAsync(CancellationToken cancellationToken);
}
