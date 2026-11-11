using Microsoft.Extensions.Logging;
using Priston.Application.Server;

namespace Priston.Application.Admin;

public sealed record GameTotals(long Accounts, int Characters);

public sealed record PortalTotals(int Registrations24h, int Registrations7d, int Registrations30d, int AccountsWithConfirmedEmail,
    int NewsPublished, int NewsDrafts, int EventsLive, int EventsUpcoming,
    int TicketsActive, int TicketsWaitingStaff, int Tickets24h);

public interface IGameStatsReader
{
    /// <summary>Contas em todas as tabelas de conta do jogo e personagens distintos na LevelTable.</summary>
    Task<GameTotals> ReadAsync(CancellationToken ct);
}

public interface IPortalStatsReader
{
    Task<PortalTotals> ReadAsync(DateTimeOffset now, CancellationToken ct);
}

public sealed record AuditEntry(long Id, DateTimeOffset OccurredUtc, string ActorType, string? ActorId, string Action, string? TargetType, string? TargetId, string? IpAddress, string? Details);

public sealed record AuditFilter(string? ActorType, string? Actor, string? ActionPrefix);

public interface IAuditReader
{
    Task<(IReadOnlyList<AuditEntry> Items, int Total)> ListAsync(AuditFilter filter, int skip, int take, CancellationToken ct);
}

public sealed record Dashboard(
    GameTotals? Game,
    PortalTotals? Portal,
    ServerStatus Server,
    DateTimeOffset GeneratedUtc);

/// <summary>Números do painel, todos lidos das fontes reais. Fonte indisponível vem como null, nunca como zero.</summary>
public sealed partial class DashboardService(IGameStatsReader game, IPortalStatsReader portal, IServerStatusProvider status, TimeProvider clock, ILogger<DashboardService> logger)
{
    public async Task<Dashboard> GetAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var statusTask = status.GetAsync(ct);
        GameTotals? g = null;
        PortalTotals? p = null;
        try
        {
            g = await game.ReadAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSourceFailed(logger, "jogo", ex);
        }

        try
        {
            p = await portal.ReadAsync(now, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSourceFailed(logger, "portal", ex);
        }

        return new Dashboard(g, p, await statusTask.ConfigureAwait(false), now);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Painel: fonte {Source} indisponível")]
    private static partial void LogSourceFailed(ILogger logger, string source, Exception ex);
}

public sealed class AuditQueryService(IAuditReader reader)
{
    public const int PageSize = 50;

    public async Task<AdminList<AuditEntry>> ListAsync(AuditFilter filter, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var clean = new AuditFilter(
            filter.ActorType is "staff" or "player" or "system" ? filter.ActorType : null,
            string.IsNullOrWhiteSpace(filter.Actor) ? null : filter.Actor.Trim()[..Math.Min(filter.Actor.Trim().Length, 64)],
            string.IsNullOrWhiteSpace(filter.ActionPrefix) ? null : filter.ActionPrefix.Trim()[..Math.Min(filter.ActionPrefix.Trim().Length, 100)]);
        var (items, total) = await reader.ListAsync(clean, (page - 1) * PageSize, PageSize, ct).ConfigureAwait(false);
        return new(items, total, page, PageSize);
    }
}
