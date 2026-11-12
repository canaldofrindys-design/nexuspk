namespace Priston.Application.Server;

public enum ServerAvailability
{
    /// <summary>Sem host/porta configurados: o portal não afirma nada.</summary>
    Unknown,
    Online,
    Offline,

    /// <summary>Parte dos endereços testados respondeu e parte não (ex.: login no ar, um canal fora).</summary>
    Partial,
}

/// <summary>
/// <paramref name="PlayersOnline"/>: contas com <c>inuse = '1'</c> nas tabelas de conta do jogo, só com o servidor
/// no ar. Nulo sem servidor configurado, com ele fora do ar ou se o banco não responder (DATABASE_MAPPING.md, seção 8).
/// </summary>
public sealed record ServerStatus(ServerAvailability Availability, DateTimeOffset? CheckedAtUtc, int? PlayersOnline);

/// <summary>Contas marcadas como em uso pelo servidor do jogo (<c>&lt;letra&gt;GameUser.inuse = '1'</c>).</summary>
public interface IOnlinePlayersReader
{
    Task<int> CountAsync(CancellationToken ct);
}

public interface IServerStatusProvider
{
    Task<ServerStatus> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Um endereço testado: o servidor de login ou um canal.</summary>
public sealed record ProbeTarget(string Key, string Name);

public sealed record TargetStatus(string Key, string Name, bool Online, int? LatencyMs);

public sealed record ServerSnapshot(ServerAvailability Availability, DateTimeOffset? CheckedAtUtc, IReadOnlyList<TargetStatus> Targets);

/// <summary>Teste de porta em cada endereço configurado.</summary>
public interface IServerProbe : IServerStatusProvider
{
    IReadOnlyList<ProbeTarget> Targets { get; }

    /// <summary>Resultado em cache por 30 segundos (página de status e início).</summary>
    Task<ServerSnapshot> GetSnapshotAsync(CancellationToken ct);

    /// <summary>Testa agora, sem cache (usado pelo Worker para o histórico).</summary>
    Task<ServerSnapshot> ProbeNowAsync(CancellationToken ct);

    public static ServerAvailability Combine(IReadOnlyCollection<TargetStatus> targets) =>
        targets.Count == 0 ? ServerAvailability.Unknown
        : targets.All(t => t.Online) ? ServerAvailability.Online
        : targets.Any(t => t.Online) ? ServerAvailability.Partial
        : ServerAvailability.Offline;
}

/// <summary>Amostras de uma hora para um endereço.</summary>
public sealed record ProbeHour(string TargetKey, DateTimeOffset HourUtc, int Samples, int OnlineSamples);

public interface IServerProbeStore
{
    Task RecordAsync(ServerSnapshot snapshot, CancellationToken ct);

    Task<IReadOnlyList<ProbeHour>> HourlyAsync(DateTimeOffset sinceUtc, CancellationToken ct);

    /// <summary>Primeira amostra gravada: o histórico só existe a partir dela.</summary>
    Task<DateTimeOffset?> FirstSampleAsync(CancellationToken ct);

    Task<int> PruneAsync(DateTimeOffset beforeUtc, CancellationToken ct);
}
