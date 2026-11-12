using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.Server;

public static class NoticeKinds
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["aviso"] = "Aviso",
        ["manutencao"] = "Manutenção",
    };
}

public sealed record ServerNotice(int Id, string Kind, string Title, string Body, DateTimeOffset StartsUtc, DateTimeOffset? EndsUtc, string Status,
    DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, string? CreatedBy, string? UpdatedBy);

public sealed record NoticeInput(string? Kind, string? Title, string? Body, string? StartsLocal, string? EndsLocal, string? Status);

public sealed record NoticeData(string Kind, string Title, string Body, DateTimeOffset StartsUtc, DateTimeOffset? EndsUtc, string Status);

public interface IServerNoticeStore
{
    Task<(IReadOnlyList<ServerNotice> Items, int Total)> ListAsync(int skip, int take, CancellationToken ct);

    Task<ServerNotice?> GetAsync(int id, CancellationToken ct);

    Task<int> CreateAsync(NoticeData data, string by, CancellationToken ct);

    Task UpdateAsync(int id, NoticeData data, string by, CancellationToken ct);

    /// <summary>Publicados que ainda não terminaram em <paramref name="now"/>.</summary>
    Task<IReadOnlyList<ServerNotice>> ListCurrentAsync(DateTimeOffset now, CancellationToken ct);
}

public sealed record PublicNotice(int Id, string Kind, string Title, string Body, DateTimeOffset StartsUtc, DateTimeOffset? EndsUtc, bool Active);

public sealed record HourCell(DateTimeOffset HourUtc, double? Online);

public sealed record TargetReport(string Key, string Name, bool? Online, int? LatencyMs, double? Uptime24h, double? Uptime7d, IReadOnlyList<HourCell> Last24h);

public sealed record StatusReport(
    ServerAvailability Availability,
    DateTimeOffset? CheckedUtc,
    IReadOnlyList<TargetReport> Targets,
    DateTimeOffset? HistorySinceUtc,
    int? PlayersOnline,
    bool MaintenanceNow,
    IReadOnlyList<PublicNotice> Notices,
    DateTimeOffset GeneratedUtc);

/// <summary>
/// Página de status: teste de porta ao vivo, histórico gravado pelo Worker e avisos da equipe.
/// Jogadores online: contas em uso no banco do jogo, só com o servidor no ar (ver <see cref="ServerStatus"/>).
/// </summary>
public sealed class StatusPageService(IServerProbe probe, IServerProbeStore history, IServerNoticeStore notices, IMemoryCache cache, TimeProvider clock)
{
    private static readonly TimeSpan HistoryCache = TimeSpan.FromSeconds(60);

    public async Task<StatusReport> GetAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var live = await probe.GetSnapshotAsync(ct).ConfigureAwait(false);
        var (hours, since, current) = (await cache.GetOrCreateAsync("status:history", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = HistoryCache;
            var h = await history.HourlyAsync(now.AddDays(-7), ct).ConfigureAwait(false);
            var first = await history.FirstSampleAsync(ct).ConfigureAwait(false);
            var n = await notices.ListCurrentAsync(now, ct).ConfigureAwait(false);
            return (h, first, n);
        }).ConfigureAwait(false));

        var thisHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var byTarget = hours.ToLookup(h => h.TargetKey);
        var targets = probe.Targets.Select(t =>
        {
            var rows = byTarget[t.Key].ToList();
            var liveTarget = live.Targets.FirstOrDefault(x => x.Key == t.Key);
            var cells = Enumerable.Range(0, 24)
                .Select(i => thisHour.AddHours(i - 23))
                .Select(hour =>
                {
                    var row = rows.FirstOrDefault(r => r.HourUtc == hour);
                    return new HourCell(hour, row is null || row.Samples == 0 ? null : (double)row.OnlineSamples / row.Samples);
                })
                .ToList();
            return new TargetReport(t.Key, t.Name, liveTarget?.Online, liveTarget?.LatencyMs,
                Uptime(rows.Where(r => r.HourUtc > thisHour.AddHours(-24))), Uptime(rows), cells);
        }).ToList();

        var publicNotices = current
            .Where(n => n.Status == "published" && (n.EndsUtc is null || n.EndsUtc > now))
            .OrderByDescending(n => n.StartsUtc <= now)
            .ThenBy(n => n.StartsUtc)
            .Select(n => new PublicNotice(n.Id, n.Kind, n.Title, n.Body, n.StartsUtc, n.EndsUtc, n.StartsUtc <= now))
            .ToList();
        var maintenance = publicNotices.Any(n => n.Active && n.Kind == "manutencao");

        var players = (await probe.GetAsync(ct).ConfigureAwait(false)).PlayersOnline;

        return new StatusReport(live.Availability, live.CheckedAtUtc, targets, since, players, maintenance, publicNotices, now);
    }

    /// <summary>Percentual de amostras online. Sem amostras, null: o portal não afirma disponibilidade que não mediu.</summary>
    public static double? Uptime(IEnumerable<ProbeHour> rows)
    {
        long samples = 0, online = 0;
        foreach (var r in rows)
        {
            samples += r.Samples;
            online += r.OnlineSamples;
        }

        return samples == 0 ? null : (double)online / samples;
    }

    public void Invalidate() => cache.Remove("status:history");
}

/// <summary>Cadastro de avisos e manutenções pela equipe (mesma permissão de notícias e eventos).</summary>
public sealed class ServerNoticeAdminService(IServerNoticeStore store, StatusPageService status, IAuditLog audit)
{
    public const int PageSize = 25;
    private static readonly string[] Statuses = ["draft", "published", "cancelled"];

    public async Task<AdminList<ServerNotice>> ListAsync(int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var (items, total) = await store.ListAsync((page - 1) * PageSize, PageSize, ct).ConfigureAwait(false);
        return new(items, total, page, PageSize);
    }

    public Task<ServerNotice?> GetAsync(int id, CancellationToken ct) => store.GetAsync(id, ct);

    public async Task<SaveResult> SaveAsync(StaffPrincipal me, int? id, NoticeInput input, ClientInfo client, CancellationToken ct)
    {
        ServerNotice? before = null;
        if (id is not null && (before = await store.GetAsync(id.Value, ct).ConfigureAwait(false)) is null)
        {
            return new(StaffAuthStatus.NotFound);
        }

        var errors = new Dictionary<string, string>();
        if (input.Kind is null || !NoticeKinds.Labels.ContainsKey(input.Kind))
        {
            errors["kind"] = "Escolha o tipo.";
        }

        var title = input.Title?.Trim() ?? string.Empty;
        if (title.Length is 0 or > 160)
        {
            errors["title"] = title.Length == 0 ? "Campo obrigatório." : "Use no máximo 160 caracteres.";
        }

        var body = input.Body?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() ?? string.Empty;
        if (body.Length is 0 or > 2000)
        {
            errors["body"] = body.Length == 0 ? "Campo obrigatório." : "Use no máximo 2.000 caracteres.";
        }

        if (input.Status is null || !Statuses.Contains(input.Status))
        {
            errors["status"] = "Escolha a situação.";
        }

        DateTimeOffset? starts = null, ends = null;
        try
        {
            starts = ContentAdminService.ParseLocal(input.StartsLocal);
            if (starts is null)
            {
                errors["startsLocal"] = "Informe o início.";
            }
        }
        catch (FormatException)
        {
            errors["startsLocal"] = "Data inválida.";
        }

        try
        {
            ends = ContentAdminService.ParseLocal(input.EndsLocal);
        }
        catch (FormatException)
        {
            errors["endsLocal"] = "Data inválida.";
        }

        if (starts is not null && ends is not null && ends <= starts)
        {
            errors["endsLocal"] = "O fim precisa ser depois do início.";
        }

        if (input.Kind == "manutencao" && ends is null && !errors.ContainsKey("endsLocal"))
        {
            errors["endsLocal"] = "Manutenção precisa de previsão de fim.";
        }

        if (errors.Count > 0)
        {
            return new(StaffAuthStatus.ValidationFailed, null, errors);
        }

        var data = new NoticeData(input.Kind!, title, body, starts!.Value, ends, input.Status!);
        var savedId = id ?? 0;
        if (id is null)
        {
            savedId = await store.CreateAsync(data, me.Username, ct).ConfigureAwait(false);
        }
        else
        {
            await store.UpdateAsync(id.Value, data, me.Username, ct).ConfigureAwait(false);
        }

        await audit.WriteAsync("staff", me.Username, id is null ? "notice.created" : "notice.updated", "notice", savedId.ToString(CultureInfo.InvariantCulture),
            client.IpAddress, JsonSerializer.Serialize(new { data.Kind, data.Title, data.Status, data.StartsUtc, data.EndsUtc, beforeStatus = before?.Status }), ct).ConfigureAwait(false);
        status.Invalidate();
        return new(StaffAuthStatus.Ok, savedId);
    }
}
