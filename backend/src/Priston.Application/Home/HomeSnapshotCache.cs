namespace Priston.Application.Home;

/// <summary>
/// Guarda a última home montada e responde com ela na hora. Montar a home lê o ranking, os clãs, as patentes, o log de
/// itens e os arquivos do servidor, e com os caches frios isso passa dos 5 segundos que o site espera. Quando isso
/// acontecia, a página caía no modo "API fora": drops com ícones decorativos e "Patentes indisponíveis".
/// Uma home com mais de <see cref="FreshFor"/> é entregue mesmo assim e refeita em segundo plano (uma montagem por vez).
/// Só a primeira chamada depois de ligar a API espera a montagem; o aquecimento em segundo plano evita até essa.
/// </summary>
public sealed class HomeSnapshotCache
{
    /// <summary>Idade a partir da qual a home é refeita em segundo plano.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);

    private readonly Func<CancellationToken, Task<HomeSnapshot>> build;
    private readonly TimeProvider clock;
    private readonly Lock gate = new();
    private HomeSnapshot? last;
    private DateTimeOffset builtAt;
    private Task<HomeSnapshot>? running;

    public HomeSnapshotCache(HomeService home, TimeProvider clock)
        : this(home.GetAsync, clock)
    {
    }

    internal HomeSnapshotCache(Func<CancellationToken, Task<HomeSnapshot>> build, TimeProvider clock)
    {
        this.build = build;
        this.clock = clock;
    }

    public async Task<HomeSnapshot> GetAsync(CancellationToken ct)
    {
        HomeSnapshot? snapshot;
        bool stale;
        lock (gate)
        {
            snapshot = last;
            stale = snapshot is null || clock.GetUtcNow() - builtAt >= FreshFor;
        }

        if (snapshot is null)
        {
            return await RefreshAsync().WaitAsync(ct).ConfigureAwait(false);
        }

        if (stale)
        {
            _ = RefreshAsync().ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        return snapshot;
    }

    /// <summary>Monta a home de novo; se já tem uma montagem andando, devolve a mesma.</summary>
    public Task<HomeSnapshot> RefreshAsync()
    {
        lock (gate)
        {
            return running ??= BuildAsync();
        }
    }

    private async Task<HomeSnapshot> BuildAsync()
    {
        // Sai do lock antes de montar, para o "finally" nunca limpar a tarefa antes de ela ser guardada.
        await Task.Yield();
        try
        {
            var snapshot = await build(CancellationToken.None).ConfigureAwait(false);
            lock (gate)
            {
                last = snapshot;
                builtAt = clock.GetUtcNow();
            }

            return snapshot;
        }
        finally
        {
            lock (gate)
            {
                running = null;
            }
        }
    }
}
