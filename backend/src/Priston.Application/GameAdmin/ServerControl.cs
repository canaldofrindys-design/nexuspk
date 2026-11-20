using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

/// <summary>Programa do servidor do jogo que o painel pode ligar e reiniciar (seção GameServerControl do portal.json).</summary>
public sealed class ServerProcessOptions
{
    /// <summary>Identificador curto (letras, números e hífen). Ex.: "login", "game1".</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Caminho completo do .exe na VPS.</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    /// <summary>Pasta de trabalho. Vazio = pasta do .exe.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Espera depois de ligar este, antes do próximo, em "Ligar todos".</summary>
    public int StartDelaySeconds { get; set; } = 5;
}

public sealed class GameServerControlOptions
{
    public const string SectionName = "GameServerControl";

    /// <summary>Na ordem de ligar (login antes do jogo). Vazio desliga a página.</summary>
    public List<ServerProcessOptions> Processes { get; set; } = [];

    /// <summary>Tempo para o programa fechar sozinho antes de ser encerrado à força.</summary>
    public int StopTimeoutSeconds { get; set; } = 30;

    /// <summary>Intervalo mínimo entre duas ações no mesmo programa.</summary>
    public int CooldownSeconds { get; set; } = 30;
}

public enum ProcessState
{
    Stopped,
    Running,

    /// <summary>Rodando, mas aberto por outra conta do Windows: o painel não consegue fechar.</summary>
    RunningOtherOwner,

    /// <summary>O .exe configurado não existe.</summary>
    Missing,
}

public sealed record ServerProcessStatus(string Key, string Name, ProcessState State, int Instances, DateTimeOffset? StartedUtc, string? Note);

public sealed record ProcessActionResult(bool Ok, string Message);

public interface IServerProcessController
{
    ServerProcessStatus GetStatus(ServerProcessOptions process);

    Task<ProcessActionResult> StartAsync(ServerProcessOptions process, CancellationToken ct);

    Task<ProcessActionResult> StopAsync(ServerProcessOptions process, TimeSpan timeout, CancellationToken ct);
}

public sealed record ServerControlRequest(string? Action, string? Target, string? Confirm, string? Reason, string? Password);

/// <summary>
/// Ligar, desligar e reiniciar os programas do servidor do jogo. Só o superadministrador; pede senha, motivo e a palavra
/// do comando digitada (LIGAR, DESLIGAR, REINICIAR). Uma ação por vez e um intervalo mínimo por programa.
/// </summary>
public sealed class ServerControlService(IServerProcessController controller, IOptions<GameServerControlOptions> options, StaffReauthService reauth, IAuditLog audit, TimeProvider clock)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, DateTimeOffset> LastAction = new(StringComparer.Ordinal);

    private GameServerControlOptions O => options.Value;

    public bool Enabled => O.Processes.Count > 0;

    public IReadOnlyList<ServerProcessStatus> Status() =>
        Valid().Select(controller.GetStatus).ToList();

    public static string ConfirmWordFor(string action) => action switch
    {
        "start" => "LIGAR",
        "stop" => "DESLIGAR",
        _ => "REINICIAR",
    };

    public async Task<GameAdminResult> RunAsync(StaffPrincipal me, ServerControlRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!Enabled)
        {
            return GameAdminResult.Disabled("Nenhum programa configurado em GameServerControl:Processes no portal.json. Veja docs/ADMIN.md.");
        }

        var errors = new Dictionary<string, string>();
        var action = r.Action is "start" or "stop" or "restart" ? r.Action : null;
        if (action is null)
        {
            errors["action"] = "Ação inválida.";
        }

        var all = r.Target == "*";
        var targets = all ? Valid().ToList() : Valid().Where(p => p.Key == r.Target).ToList();
        if (targets.Count == 0)
        {
            errors["target"] = "Escolha o programa.";
        }

        if (action is not null && !string.Equals(r.Confirm?.Trim(), ConfirmWordFor(action), StringComparison.Ordinal))
        {
            errors["confirm"] = $"Digite {ConfirmWordFor(action)} para confirmar.";
        }

        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        if (!await reauth.VerifyAsync(me, r.Password, client, ct).ConfigureAwait(false))
        {
            return GameAdminResult.WrongPassword;
        }

        if (!await Gate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return GameAdminResult.Conflict("Outra ação no servidor está em andamento. Aguarde terminar.");
        }

        try
        {
            var now = clock.GetUtcNow();
            foreach (var t in targets)
            {
                if (LastAction.TryGetValue(t.Key, out var last) && now - last < TimeSpan.FromSeconds(O.CooldownSeconds))
                {
                    return GameAdminResult.Conflict($"{t.Name} recebeu um comando agora há pouco. Espere {O.CooldownSeconds} segundos entre comandos.");
                }
            }

            var messages = new List<string>();
            var ok = true;
            var timeout = TimeSpan.FromSeconds(Math.Clamp(O.StopTimeoutSeconds, 5, 300));

            // Desligar segue a ordem inversa (jogo antes do login); ligar segue a ordem configurada.
            if (action is "stop" or "restart")
            {
                foreach (var t in Enumerable.Reverse(targets))
                {
                    var res = await controller.StopAsync(t, timeout, ct).ConfigureAwait(false);
                    ok &= res.Ok;
                    messages.Add($"{t.Name}: {res.Message}");
                    LastAction[t.Key] = clock.GetUtcNow();
                }
            }

            if (action is "start" or "restart" && ok)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    var res = await controller.StartAsync(t, ct).ConfigureAwait(false);
                    ok &= res.Ok;
                    messages.Add($"{t.Name}: {res.Message}");
                    LastAction[t.Key] = clock.GetUtcNow();
                    if (!res.Ok)
                    {
                        break;
                    }

                    if (i < targets.Count - 1 && t.StartDelaySeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(t.StartDelaySeconds, 120)), clock, ct).ConfigureAwait(false);
                    }
                }
            }

            await GameAudit.WriteAsync(audit, me, $"game.server.{action}", "server-process", all ? "*" : targets[0].Key, client,
                new { targets = targets.Select(t => t.Key), ok, result = messages, reason }, ct).ConfigureAwait(false);
            var text = string.Join(" ", messages);
            return ok ? GameAdminResult.Ok(null, text) : GameAdminResult.Conflict(text);
        }
        finally
        {
            Gate.Release();
        }
    }

    private IEnumerable<ServerProcessOptions> Valid() =>
        O.Processes.Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Key, "^[a-z0-9-]{1,32}$") && !string.IsNullOrWhiteSpace(p.ExecutablePath));
}
