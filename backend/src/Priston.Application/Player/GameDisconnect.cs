using Priston.Application.Accounts;

namespace Priston.Application.Player;

/// <summary>Situação gravada pelo servidor do jogo em PortalDB.dbo.GameDisconnectRequest.Status.</summary>
public static class DisconnectStatus
{
    public const string Pending = "pending";
    public const string Done = "done";
    public const string NotOnline = "not-online";
    public const string Failed = "failed";

    /// <summary>Só o portal usa: pedido pendente que o servidor não atendeu a tempo.</summary>
    public const string Expired = "expired";
}

/// <summary>Uma linha de GameDisconnectRequest.</summary>
public sealed record DisconnectRequestRow(long Id, DateTimeOffset RequestedUtc, string Status, DateTimeOffset? ProcessedUtc, string? ServerNote);

/// <summary>Sinal de vida do servidor do jogo para a função de desconectar (GameServerRuntime, Feature = 'disconnect').</summary>
public sealed record DisconnectRuntime(int ProtocolVersion, DateTimeOffset? LastSeenUtc);

/// <summary>O que o painel mostra: se dá para pedir agora e o último pedido da conta.</summary>
public sealed record DisconnectState(bool Available, string? UnavailableReason, DisconnectRequestView? Last, DateTimeOffset? NextAllowedUtc);

public sealed record DisconnectRequestView(long Id, DateTimeOffset RequestedUtc, string Status, DateTimeOffset? ProcessedUtc);

public enum DisconnectOutcome
{
    Ok,
    Unavailable,
    AlreadyPending,
    TooMany,
}

public sealed record DisconnectResult(DisconnectOutcome Outcome, string? Message, DisconnectState State);

/// <summary>Fila de pedidos de desconexão no banco do portal. O servidor do jogo lê e responde (docs/DESCONECTAR.md).</summary>
public interface IGameDisconnectStore
{
    Task<DisconnectRuntime?> RuntimeAsync(CancellationToken ct);

    Task<DisconnectRequestRow?> LatestAsync(string gameLogin, CancellationToken ct);

    Task<int> CountSinceAsync(string gameLogin, DateTimeOffset sinceUtc, CancellationToken ct);

    /// <summary>
    /// Grava o pedido, a não ser que a conta já tenha um pedido pendente feito depois de <paramref name="pendingSinceUtc"/>.
    /// Devolve o Id, ou null quando já havia um pendente. A conferência e a gravação ficam na mesma transação.
    /// </summary>
    Task<long?> TryCreateAsync(string gameLogin, DateTimeOffset nowUtc, DateTimeOffset pendingSinceUtc, string? ipAddress, CancellationToken ct);
}

/// <summary>
/// "Desconectar personagem travado" do painel do jogador. Só a própria conta, um pedido por vez, no máximo
/// <see cref="MaxPerHour"/> por hora, e só com o servidor do jogo respondendo.
/// </summary>
public sealed class GameDisconnectService(IGameDisconnectStore store, IAuditLog audit, TimeProvider clock)
{
    /// <summary>Versão do contrato com o servidor do jogo (docs/DESCONECTAR.md).</summary>
    public const int ProtocolVersion = 1;

    /// <summary>O servidor marca o sinal a cada poucos segundos; mais que isso sem sinal, o botão desliga.</summary>
    public static readonly TimeSpan ServerSilenceLimit = TimeSpan.FromMinutes(2);

    /// <summary>Pedido pendente há mais que isso: o servidor não atende mais (ele ignora pedidos antigos).</summary>
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(2);

    public const int MaxPerHour = 5;

    public const string ServerOffMessage =
        "O servidor do jogo não está atendendo pedidos de desconexão agora. Tente de novo em alguns minutos ou abra um chamado no suporte.";

    public const string PendingMessage = "Já existe um pedido em andamento. Aguarde alguns segundos.";

    public static readonly string TooManyMessage =
        $"Limite de {MaxPerHour} pedidos por hora. Se o personagem continua travado, abra um chamado no suporte.";

    public async Task<DisconnectState> GetAsync(string login, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var runtime = await store.RuntimeAsync(ct).ConfigureAwait(false);
        var last = await store.LatestAsync(login, ct).ConfigureAwait(false);
        var count = await store.CountSinceAsync(login, now.AddHours(-1), ct).ConfigureAwait(false);
        return Build(runtime, last, count, now);
    }

    public async Task<DisconnectResult> RequestAsync(string login, ClientInfo client, CancellationToken ct)
    {
        var state = await GetAsync(login, ct).ConfigureAwait(false);
        if (!state.Available)
        {
            return state.UnavailableReason == ServerOffMessage ? new(DisconnectOutcome.Unavailable, ServerOffMessage, state)
                : state.Last?.Status == DisconnectStatus.Pending ? new(DisconnectOutcome.AlreadyPending, PendingMessage, state)
                : new(DisconnectOutcome.TooMany, TooManyMessage, state);
        }

        var now = clock.GetUtcNow();
        var id = await store.TryCreateAsync(login, now, now - PendingTimeout, client.IpAddress, ct).ConfigureAwait(false);
        if (id is null)
        {
            return new(DisconnectOutcome.AlreadyPending, PendingMessage, await GetAsync(login, ct).ConfigureAwait(false));
        }

        await audit.WriteAsync("player", login, "player.game.disconnect-requested", "game-account", login, client.IpAddress, null, ct).ConfigureAwait(false);
        return new(DisconnectOutcome.Ok, null, await GetAsync(login, ct).ConfigureAwait(false));
    }

    public static bool ServerAnswering(DisconnectRuntime? runtime, DateTimeOffset now) =>
        runtime is { LastSeenUtc: { } seen } && runtime.ProtocolVersion >= ProtocolVersion && now - seen < ServerSilenceLimit;

    public static DisconnectState Build(DisconnectRuntime? runtime, DisconnectRequestRow? last, int countLastHour, DateTimeOffset now)
    {
        var view = last is null ? null : new DisconnectRequestView(last.Id, last.RequestedUtc, EffectiveStatus(last, now), last.ProcessedUtc);
        var answering = ServerAnswering(runtime, now);

        // Com o limite batido, o próximo pedido libera uma hora depois do último (aproximação simples e conservadora).
        DateTimeOffset? next = countLastHour >= MaxPerHour && last is not null ? last.RequestedUtc.AddHours(1) : null;

        string? reason = !answering ? ServerOffMessage
            : view?.Status == DisconnectStatus.Pending ? PendingMessage
            : next is { } n && n > now ? TooManyMessage
            : null;

        return new(reason is null, reason, view, next is { } x && x > now ? x : null);
    }

    /// <summary>Pendente há mais de <see cref="PendingTimeout"/> vira "expirado": o servidor não vai mais atender.</summary>
    public static string EffectiveStatus(DisconnectRequestRow row, DateTimeOffset now) =>
        row.Status == DisconnectStatus.Pending && now - row.RequestedUtc >= PendingTimeout ? DisconnectStatus.Expired : row.Status;
}
