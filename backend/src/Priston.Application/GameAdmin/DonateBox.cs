using System.Text.RegularExpressions;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Domain.Characters;

namespace Priston.Application.GameAdmin;

public sealed record DonateBoxItemInput(string? Code, int? Quantity, int? Aging, bool? Mix);

public sealed record DonateBoxRequest(string? Login, string? Character, IReadOnlyList<DonateBoxItemInput>? Items, string? Reason);

/// <summary>Uma linha de item do pacote (NexusDonateItems, Ordinal 1..64).</summary>
public sealed record DonateBoxLine(string Code, int Aging, bool Mix);

/// <summary>Pacote pronto para gravar: a conta, o personagem que vai abrir, a classe (Spec 1..11) e os itens.</summary>
public sealed record DonateBoxPackage(string Login, string Character, int Spec, string RequestKey, string CreatedBy, string Reason, IReadOnlyList<DonateBoxLine> Lines);

/// <summary>Pacote já gravado, com a situação que o servidor do jogo deu a ele.</summary>
public sealed record DonateBoxSent(int Id, string Login, string? Character, int Spec, string Status, string? CreatedBy, string? Reason,
    DateTime? CreatedAt, DateTime? CompletedAt, bool Notified, int Items, string? FirstItem);

/// <summary>Estado do distribuidor de donate do servidor do jogo (NexusDonateRuntime).</summary>
public sealed record DonateRuntime(bool Ready, DateTime? LastSeenLocal, int ProtocolVersion);

/// <summary>
/// Pacotes de Box Donate no banco do servidor (NexusPKAdmin.dbo.NexusDonatePackages e NexusDonateItems).
/// O portal só cria o pacote, do mesmo jeito que a Coin Shop e o Login Diário; o servidor do jogo monta o item, avisa o
/// jogador e entrega a caixa quando ele entra.
/// </summary>
public interface IDonateBoxStore
{
    bool Enabled { get; }

    /// <summary>Grava o pacote, a linha de controle BI354 e os itens numa transação. Devolve o ID do pacote.</summary>
    Task<int> CreateAsync(DonateBoxPackage package, CancellationToken ct);

    Task<IReadOnlyList<DonateBoxSent>> ListRecentAsync(int take, CancellationToken ct);

    /// <summary>Null quando a tabela não existe ou está vazia.</summary>
    Task<DonateRuntime?> RuntimeAsync(CancellationToken ct);
}

/// <summary>
/// "Enviar Box Donate" do painel: cria um pacote para o personagem escolhido. Pede motivo, vai para a auditoria e só
/// aceita códigos de item no formato do jogo.
/// </summary>
public sealed partial class DonateBoxService(
    IDonateBoxStore store,
    IGameAdminAccountStore accounts,
    IAuditLog audit,
    TimeProvider clock)
{
    /// <summary>Código da caixa usada como linha de controle (Ordinal 0) pelo distribuidor do jogo.</summary>
    public const string BoxCode = "BI354";

    /// <summary>O jogo aceita até 64 itens por pacote (CHECK Ordinal 0..64).</summary>
    public const int MaxItems = 64;

    public const int MaxAging = 30;

    /// <summary>Quem criou o pacote, como aparece em NexusDonatePackages.CreatedBy.</summary>
    public const string CreatedByPrefix = "Portal";

    public const string DisabledMessage =
        "O login de administração do jogo não está configurado (GameDatabase:AdminConnectionString). Veja docs/ADMIN.md, seção \"Ligar as funções do jogo\".";

    [GeneratedRegex("^[A-Z0-9_]{1,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public bool Enabled => store.Enabled;

    public async Task<GameAdminResult> OverviewAsync(CancellationToken ct)
    {
        if (!store.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        try
        {
            var runtime = await store.RuntimeAsync(ct).ConfigureAwait(false);
            var recent = await store.ListRecentAsync(30, ct).ConfigureAwait(false);
            return GameAdminResult.Ok(new { Runtime = runtime, Online = IsOnline(runtime), Recent = recent, MaxItems, MaxAging });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return GameAdminResult.Conflict("Não consegui ler as tabelas de donate do servidor (NexusPKAdmin). Confira se o login de administração tem acesso a esse banco.");
        }
    }

    /// <summary>O distribuidor do jogo marca LastSeen enquanto está rodando. Mais de 10 minutos sem sinal: parado.</summary>
    internal bool IsOnline(DonateRuntime? r) =>
        r is { Ready: true, LastSeenLocal: { } seen } && GameTime.ToUtc(seen) is { } utc && clock.GetUtcNow() - utc < TimeSpan.FromMinutes(10);

    public async Task<GameAdminResult> SendAsync(StaffPrincipal me, DonateBoxRequest r, ClientInfo client, CancellationToken ct)
    {
        if (!store.Enabled || !accounts.Enabled)
        {
            return GameAdminResult.Disabled(DisabledMessage);
        }

        var errors = new Dictionary<string, string>();
        var reason = Reason.Clean(r.Reason, out var reasonError);
        if (reasonError is not null)
        {
            errors["reason"] = reasonError;
        }

        var login = (r.Login ?? string.Empty).Trim();
        if (!GameAccountAdminService.IsValidLogin(login))
        {
            errors["login"] = "Informe o login da conta.";
        }

        var character = (r.Character ?? string.Empty).Trim();
        if (character.Length == 0)
        {
            errors["character"] = "Escolha o personagem que vai receber.";
        }

        var lines = ParseItems(r.Items, errors);
        if (errors.Count > 0)
        {
            return GameAdminResult.Invalid(errors);
        }

        var snap = await accounts.GetAsync(login, ct).ConfigureAwait(false);
        if (snap is null)
        {
            return GameAdminResult.Invalid("login", "Conta não encontrada no jogo.");
        }

        var canonical = snap.AccountRows[0].Login.Trim();
        var target = snap.Characters.FirstOrDefault(c => string.Equals(c.Name.Trim(), character, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return GameAdminResult.Invalid("character", "Esse personagem não é desta conta.");
        }

        var spec = target.Class ?? 0;
        if (!CharacterClassNames.IsKnown(spec))
        {
            return GameAdminResult.Invalid("character", "A classe deste personagem não é uma das 11 do jogo, então o servidor não entrega a caixa para ele.");
        }

        var createdBy = Cut($"{CreatedByPrefix} ({me.Username})", 50);
        var package = new DonateBoxPackage(canonical, target.Name.Trim(), spec, $"PORTAL-{Guid.NewGuid():N}", createdBy, reason!, lines);
        int id;
        try
        {
            id = await store.CreateAsync(package, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return GameAdminResult.Conflict("O banco do servidor recusou o pacote. Nada foi enviado. Confira se o login de administração pode gravar no NexusPKAdmin.");
        }

        await GameAudit.WriteAsync(audit, me, "game.donate.sent", "game-account", canonical, client,
            new
            {
                package = id,
                character = package.Character,
                spec,
                items = lines.GroupBy(l => (l.Code, l.Aging, l.Mix)).Select(g => new { code = g.Key.Code, aging = g.Key.Aging, mix = g.Key.Mix, quantity = g.Count() }),
                reason,
            }, ct).ConfigureAwait(false);
        var count = lines.Count == 1 ? "1 item" : $"{lines.Count} itens";
        return GameAdminResult.Ok(new { Package = id },
            $"Box Donate nº {id} criada para {package.Character} ({count}). O servidor avisa o jogador e entrega a caixa quando ele entrar com esse personagem.");
    }

    /// <summary>
    /// Confere os itens. O código não é cruzado com o catálogo de arquivos: itens de donate (como os BI) podem não ter
    /// arquivo em OpenItem. A tela mostra a foto e o nome para conferir antes de enviar.
    /// </summary>
    private static List<DonateBoxLine> ParseItems(IReadOnlyList<DonateBoxItemInput>? items, Dictionary<string, string> errors)
    {
        var lines = new List<DonateBoxLine>();
        var given = items ?? [];
        if (given.All(i => string.IsNullOrWhiteSpace(i.Code)))
        {
            errors["items"] = "Coloque pelo menos um item na caixa.";
            return lines;
        }

        for (var i = 0; i < given.Count; i++)
        {
            // Linha vazia é ignorada, mas o número das linhas continua o mesmo da tela (os erros apontam para ele).
            var item = given[i];
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                continue;
            }

            var code = item.Code.Trim().ToUpperInvariant();
            var quantity = item.Quantity ?? 1;
            var aging = item.Aging ?? 0;
            if (!CodePattern().IsMatch(code))
            {
                errors[$"items.{i}.code"] = "Código inválido: até 7 letras e números, como CA105.";
            }
            else if (code == BoxCode)
            {
                errors[$"items.{i}.code"] = "BI354 é a própria caixa: coloque os itens que vão dentro dela.";
            }

            if (quantity is < 1 or > MaxItems)
            {
                errors[$"items.{i}.quantity"] = $"Quantidade de 1 a {MaxItems}.";
            }

            if (aging is < 0 or > MaxAging)
            {
                errors[$"items.{i}.aging"] = $"Aging de 0 a {MaxAging}.";
            }

            if (quantity is >= 1 and <= MaxItems)
            {
                for (var q = 0; q < quantity; q++)
                {
                    lines.Add(new DonateBoxLine(code, Math.Clamp(aging, 0, MaxAging), item.Mix == true));
                }
            }
        }

        if (lines.Count > MaxItems)
        {
            errors["items"] = $"Uma caixa leva no máximo {MaxItems} itens (somando as quantidades). Agora são {lines.Count}.";
        }

        return lines;
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
