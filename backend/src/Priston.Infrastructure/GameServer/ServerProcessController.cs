using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Priston.Application.GameAdmin;

namespace Priston.Infrastructure.GameServer;

/// <summary>
/// Liga e encerra os programas do servidor do jogo na própria VPS. Só abre o .exe configurado no portal.json;
/// nenhum caminho vem da tela. O programa é procurado pelo nome do .exe e, quando o Windows deixa ler, pelo caminho completo.
/// </summary>
internal sealed partial class ServerProcessController(ILogger<ServerProcessController> logger) : IServerProcessController
{
    public ServerProcessStatus GetStatus(ServerProcessOptions p)
    {
        if (!File.Exists(p.ExecutablePath))
        {
            return new(p.Key, p.Name, ProcessState.Missing, 0, null, "O arquivo configurado não existe nesta máquina.");
        }

        var found = Find(p);
        try
        {
            if (found.Count == 0)
            {
                return new(p.Key, p.Name, ProcessState.Stopped, 0, null, null);
            }

            var foreign = found.Count(f => !f.Readable);
            DateTimeOffset? started = null;
            foreach (var f in found.Where(f => f.Readable))
            {
                try
                {
                    started = new DateTimeOffset(f.Process.StartTime.ToUniversalTime(), TimeSpan.Zero);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // sem permissão para ler a hora de início
                }
            }

            return foreign == found.Count
                ? new(p.Key, p.Name, ProcessState.RunningOtherOwner, found.Count, null, "Aberto por outra conta do Windows: o painel vê, mas não consegue fechar.")
                : new(p.Key, p.Name, ProcessState.Running, found.Count, started, found.Count > 1 ? $"{found.Count} cópias abertas." : null);
        }
        finally
        {
            foreach (var f in found)
            {
                f.Process.Dispose();
            }
        }
    }

    public Task<ProcessActionResult> StartAsync(ServerProcessOptions p, CancellationToken ct)
    {
        if (!File.Exists(p.ExecutablePath))
        {
            return Task.FromResult(new ProcessActionResult(false, "o arquivo configurado não existe."));
        }

        var running = Find(p);
        if (running.Count > 0)
        {
            foreach (var f in running)
            {
                f.Process.Dispose();
            }

            return Task.FromResult(new ProcessActionResult(true, "já estava ligado."));
        }

        try
        {
            var info = new ProcessStartInfo(p.ExecutablePath)
            {
                Arguments = p.Arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(p.WorkingDirectory) ? Path.GetDirectoryName(p.ExecutablePath)! : p.WorkingDirectory,
                UseShellExecute = false,
            };
            using var started = Process.Start(info);
            LogStarted(logger, p.Key, started?.Id ?? 0);
            return Task.FromResult(new ProcessActionResult(started is not null, started is null ? "o Windows não abriu o programa." : "ligado."));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            LogFailed(logger, p.Key, ex);
            return Task.FromResult(new ProcessActionResult(false, "o Windows recusou abrir o programa (permissão da conta do serviço na pasta do servidor)."));
        }
    }

    public async Task<ProcessActionResult> StopAsync(ServerProcessOptions p, TimeSpan timeout, CancellationToken ct)
    {
        var found = Find(p);
        if (found.Count == 0)
        {
            return new ProcessActionResult(true, "já estava desligado.");
        }

        try
        {
            if (found.Any(f => !f.Readable))
            {
                return new ProcessActionResult(false, "foi aberto por outra conta do Windows; feche-o uma vez na própria VPS e depois use o painel para ligar.");
            }

            foreach (var f in found)
            {
                try
                {
                    // Pede para fechar (janela); se não fechar no prazo, encerra.
                    f.Process.CloseMainWindow();
                }
                catch (InvalidOperationException)
                {
                    // já saiu
                }
            }

            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(timeout);
            var forced = false;
            foreach (var f in found)
            {
                try
                {
                    await f.Process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    f.Process.Kill(entireProcessTree: true);
                    await f.Process.WaitForExitAsync(ct).ConfigureAwait(false);
                    forced = true;
                }
            }

            LogStopped(logger, p.Key, forced);
            return new ProcessActionResult(true, forced ? "desligado (forçado, não fechou sozinho no prazo)." : "desligado.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            LogFailed(logger, p.Key, ex);
            return new ProcessActionResult(false, "o Windows não deixou fechar (permissão).");
        }
        finally
        {
            foreach (var f in found)
            {
                f.Process.Dispose();
            }
        }
    }

    private sealed record Found(Process Process, bool Readable);

    private static List<Found> Find(ServerProcessOptions p)
    {
        var name = Path.GetFileNameWithoutExtension(p.ExecutablePath);
        var expected = Path.GetFullPath(p.ExecutablePath);
        var list = new List<Found>();
        foreach (var proc in Process.GetProcessesByName(name))
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (path is not null && !string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase))
                {
                    proc.Dispose(); // mesmo nome, outra pasta: não é este servidor
                    continue;
                }

                list.Add(new Found(proc, true));
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                list.Add(new Found(proc, false));
            }
        }

        return list;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Servidor do jogo: {Key} ligado (pid {Pid})")]
    private static partial void LogStarted(ILogger logger, string key, int pid);

    [LoggerMessage(Level = LogLevel.Information, Message = "Servidor do jogo: {Key} desligado (forçado: {Forced})")]
    private static partial void LogStopped(ILogger logger, string key, bool forced);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Servidor do jogo: falha em {Key}")]
    private static partial void LogFailed(ILogger logger, string key, Exception ex);
}
