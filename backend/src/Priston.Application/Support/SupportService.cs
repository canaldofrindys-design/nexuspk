using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Home;

namespace Priston.Application.Support;

/// <summary>
/// Central de suporte. O jogador só enxerga os próprios chamados e nunca vê notas internas;
/// a equipe com a permissão de suporte vê a fila inteira. Toda mudança vai para a auditoria (sem o texto das mensagens).
/// </summary>
public sealed partial class SupportService(
    ISupportStore store,
    IPlayerAccountRepository players,
    IEmailSender email,
    IAuditLog audit,
    TimeProvider clock,
    IOptions<ServerPresentationOptions> presentation,
    IOptions<AccountOptions> accountOptions,
    ILogger<SupportService> logger)
{
    public const int MaxActiveTicketsPerPlayer = 3;
    public const int MaxFilesPerMessage = 3;
    public const int BodyMaxLength = 4000;
    public const int SubjectMaxLength = 120;
    public const int PlayerPageSize = 20;
    public const int QueuePageSize = 30;

    // ---------- Jogador ----------

    public async Task<TicketList> ListMineAsync(string login, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var (items, total) = await store.ListByPlayerAsync(login, (page - 1) * PlayerPageSize, PlayerPageSize, ct).ConfigureAwait(false);
        return new(items, total, page, PlayerPageSize);
    }

    public async Task<SupportResult> OpenAsync(string login, NewTicketRequest request, IReadOnlyList<UploadedFile> files, ClientInfo client, CancellationToken ct)
    {
        var errors = new Dictionary<string, string>();
        if (!TicketCategories.IsValid(request.Category))
        {
            errors["category"] = "Escolha uma categoria.";
        }

        var subject = Text(request.Subject, SubjectMaxLength, 5, "subject", errors);
        var body = Text(request.Body, BodyMaxLength, 20, "body", errors);
        var clean = await FilesAsync(files, errors, ct).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            return new(SupportStatus.ValidationFailed, null, errors);
        }

        if (await store.CountActiveAsync(login, ct).ConfigureAwait(false) >= MaxActiveTicketsPerPlayer)
        {
            return new(SupportStatus.TooManyOpen);
        }

        var id = await store.CreateAsync(login, request.Category!, subject!, body!, clean, clock.GetUtcNow(), ct).ConfigureAwait(false);
        await Audit("player", login, "ticket.created", id, client, new { request.Category, attachments = clean.Count }, ct).ConfigureAwait(false);
        return new(SupportStatus.Ok, id);
    }

    /// <summary>Chamado do próprio jogador, sem notas internas. Abrir marca as respostas como lidas.</summary>
    public async Task<TicketDetail?> GetMineAsync(string login, int id, CancellationToken ct)
    {
        var ticket = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (ticket is null || !SameLogin(ticket.GameLogin, login))
        {
            return null;
        }

        if (ticket.PlayerUnread)
        {
            await store.MarkPlayerReadAsync(id, ct).ConfigureAwait(false);
            ticket = ticket with { PlayerUnread = false };
        }

        return await DetailAsync(ticket, includeInternal: false, ct).ConfigureAwait(false);
    }

    public async Task<SupportResult> PlayerReplyAsync(string login, int id, string? body, IReadOnlyList<UploadedFile> files, ClientInfo client, CancellationToken ct)
    {
        var ticket = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (ticket is null || !SameLogin(ticket.GameLogin, login))
        {
            return new(SupportStatus.NotFound);
        }

        if (ticket.Status == TicketStatuses.Closed)
        {
            return new(SupportStatus.Closed);
        }

        var errors = new Dictionary<string, string>();
        var text = Text(body, BodyMaxLength, 2, "body", errors);
        var clean = await FilesAsync(files, errors, ct).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            return new(SupportStatus.ValidationFailed, id, errors);
        }

        // Resposta do jogador devolve o chamado para a fila; "em atendimento" continua com quem está atendendo.
        var next = ticket.Status == TicketStatuses.InProgress ? TicketStatuses.InProgress : TicketStatuses.Open;
        var now = clock.GetUtcNow();
        await store.AddMessageAsync(id, "jogador", null, text!, false, clean, new TicketTransition(next, "jogador", false, now, null), ct).ConfigureAwait(false);
        await Audit("player", login, "ticket.player-replied", id, client, new { from = ticket.Status, to = next, attachments = clean.Count }, ct).ConfigureAwait(false);
        return new(SupportStatus.Ok, id);
    }

    public async Task<SupportResult> PlayerCloseAsync(string login, int id, ClientInfo client, CancellationToken ct)
    {
        var ticket = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (ticket is null || !SameLogin(ticket.GameLogin, login))
        {
            return new(SupportStatus.NotFound);
        }

        if (ticket.Status != TicketStatuses.Closed)
        {
            await store.UpdateAsync(id, TicketStatuses.Closed, ticket.Priority, ticket.AssignedStaffId, "jogador", clock.GetUtcNow(), ct).ConfigureAwait(false);
            await Audit("player", login, "ticket.closed", id, client, new { from = ticket.Status }, ct).ConfigureAwait(false);
        }

        return new(SupportStatus.Ok, id);
    }

    public async Task<AttachmentContent?> GetMyAttachmentAsync(string login, int ticketId, Guid attachmentId, CancellationToken ct)
    {
        var ticket = await store.GetAsync(ticketId, ct).ConfigureAwait(false);
        return ticket is null || !SameLogin(ticket.GameLogin, login)
            ? null
            : await store.GetAttachmentAsync(ticketId, attachmentId, includeInternal: false, ct).ConfigureAwait(false);
    }

    // ---------- Equipe ----------

    public async Task<TicketList> QueueAsync(TicketQueueFilter filter, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var clean = new TicketQueueFilter(
            filter.Status == "ativos" || TicketStatuses.IsValid(filter.Status) ? filter.Status : null,
            TicketCategories.IsValid(filter.Category) ? filter.Category : null,
            filter.AssignedStaffId,
            string.IsNullOrWhiteSpace(filter.GameLogin) ? null : filter.GameLogin.Trim()[..Math.Min(32, filter.GameLogin.Trim().Length)],
            filter.WaitingStaff);
        var (items, total) = await store.ListQueueAsync(clean, (page - 1) * QueuePageSize, QueuePageSize, ct).ConfigureAwait(false);
        return new(items, total, page, QueuePageSize);
    }

    public async Task<TicketDetail?> GetForStaffAsync(int id, CancellationToken ct)
    {
        var ticket = await store.GetAsync(id, ct).ConfigureAwait(false);
        return ticket is null ? null : await DetailAsync(ticket, includeInternal: true, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SupportAgent>> AgentsAsync(CancellationToken ct) => store.ListAgentsAsync(SupportRoles(), ct);

    public async Task<SupportResult> StaffReplyAsync(StaffPrincipal me, int id, StaffReplyRequest request, IReadOnlyList<UploadedFile> files, ClientInfo client, CancellationToken ct)
    {
        var ticket = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (ticket is null)
        {
            return new(SupportStatus.NotFound);
        }

        var errors = new Dictionary<string, string>();
        var text = Text(request.Body, BodyMaxLength, 2, "body", errors);
        var statusAfter = string.IsNullOrEmpty(request.StatusAfter) ? null : request.StatusAfter;
        if (statusAfter is not null && !TicketStatuses.IsValid(statusAfter))
        {
            errors["statusAfter"] = "Situação inválida.";
        }

        var clean = await FilesAsync(files, errors, ct).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            return new(SupportStatus.ValidationFailed, id, errors);
        }

        var now = clock.GetUtcNow();
        TicketTransition transition;
        if (request.Internal)
        {
            // Nota interna não muda o que o jogador vê, só a situação se a equipe pedir.
            var status = statusAfter ?? ticket.Status;
            transition = new(status, ticket.LastAuthor, ticket.PlayerUnread, now, status == TicketStatuses.Closed ? me.Username : null);
        }
        else
        {
            var status = statusAfter ?? TicketStatuses.WaitingPlayer;
            transition = new(status, "equipe", true, now, status == TicketStatuses.Closed ? me.Username : null);
        }

        await store.AddMessageAsync(id, "equipe", me.Id, text!, request.Internal, clean, transition, ct).ConfigureAwait(false);
        await Audit("staff", me.Username, request.Internal ? "ticket.note" : "ticket.replied", id, client,
            new { from = ticket.Status, to = transition.Status, attachments = clean.Count }, ct).ConfigureAwait(false);
        if (!request.Internal)
        {
            await NotifyPlayerAsync(ticket, ct).ConfigureAwait(false);
        }

        return new(SupportStatus.Ok, id);
    }

    public async Task<SupportResult> UpdateAsync(StaffPrincipal me, int id, TicketUpdateRequest request, ClientInfo client, CancellationToken ct)
    {
        var ticket = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (ticket is null)
        {
            return new(SupportStatus.NotFound);
        }

        var errors = new Dictionary<string, string>();
        var status = request.Status ?? ticket.Status;
        if (!TicketStatuses.IsValid(status))
        {
            errors["status"] = "Situação inválida.";
        }

        var priority = request.Priority ?? ticket.Priority;
        if (!TicketPriorities.IsValid(priority))
        {
            errors["priority"] = "Prioridade inválida.";
        }

        if (request.AssignedStaffId is { } staffId && (await AgentsAsync(ct).ConfigureAwait(false)).All(a => a.Id != staffId))
        {
            errors["assignedStaffId"] = "Escolha um membro ativo da equipe de suporte.";
        }

        if (errors.Count > 0)
        {
            return new(SupportStatus.ValidationFailed, id, errors);
        }

        var closedBy = status == TicketStatuses.Closed ? (ticket.Status == TicketStatuses.Closed ? ticket.ClosedBy : me.Username) : null;
        await store.UpdateAsync(id, status, priority, request.AssignedStaffId, closedBy, clock.GetUtcNow(), ct).ConfigureAwait(false);
        await Audit("staff", me.Username, "ticket.updated", id, client, new
        {
            from = new { ticket.Status, ticket.Priority, ticket.AssignedStaffId },
            to = new { Status = status, Priority = priority, request.AssignedStaffId },
        }, ct).ConfigureAwait(false);
        return new(SupportStatus.Ok, id);
    }

    public Task<AttachmentContent?> GetAttachmentForStaffAsync(int ticketId, Guid attachmentId, CancellationToken ct) =>
        store.GetAttachmentAsync(ticketId, attachmentId, includeInternal: true, ct);

    public EmailMessage BuildReplyEmail(string to, TicketSummary ticket)
    {
        var server = presentation.Value.Name;
        var url = $"{accountOptions.Value.PublicBaseUrl.TrimEnd('/')}/painel/suporte/{ticket.Id}";
        var body = string.Join("\n",
            "Olá,",
            "",
            $"A equipe respondeu o seu chamado #{ticket.Id} (\"{ticket.Subject}\").",
            "",
            $"Leia a resposta e continue a conversa em: {url}",
            "",
            "Por segurança, a resposta não vem neste e-mail. A equipe nunca pede a sua senha.",
            "",
            $"Equipe {server}");
        return new EmailMessage(to, $"{server}: resposta no chamado #{ticket.Id}", body);
    }

    private static List<string> SupportRoles() =>
        StaffRoles.Labels.Keys.Where(r => StaffRoles.Can(r, Capability.ManageSupport)).ToList();

    private async Task NotifyPlayerAsync(TicketSummary ticket, CancellationToken ct)
    {
        try
        {
            if (!email.IsConfigured)
            {
                return;
            }

            var account = await players.FindAsync(ticket.GameLogin, ct).ConfigureAwait(false);
            if (account?.Email is null || account.EmailConfirmedUtc is null)
            {
                return;
            }

            await email.SendAsync(BuildReplyEmail(account.Email, ticket), ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A resposta já foi gravada; falha no e-mail só fica no log.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogNotifyFailed(logger, ticket.Id, ex);
        }
    }

    private async Task<TicketDetail> DetailAsync(TicketSummary ticket, bool includeInternal, CancellationToken ct)
    {
        var messages = await store.ListMessagesAsync(ticket.Id, includeInternal, ct).ConfigureAwait(false);
        var attachments = (await store.ListAttachmentsAsync(ticket.Id, ct).ConfigureAwait(false)).ToLookup(a => a.MessageId);
        var views = messages.Select(m => new TicketMessageView(
            m.Id,
            m.AuthorType,
            // O jogador vê o nome de exibição do atendente, nunca o usuário de login da equipe.
            m.StaffName,
            m.Body,
            m.Internal,
            m.CreatedUtc,
            attachments[m.Id].ToList())).ToList();
        return new TicketDetail(ticket, views);
    }

    private static async Task<IReadOnlyList<CleanAttachment>> FilesAsync(IReadOnlyList<UploadedFile> files, Dictionary<string, string> errors, CancellationToken ct)
    {
        var real = files.Where(f => f.Length > 0).ToList();
        if (real.Count > MaxFilesPerMessage)
        {
            errors["files"] = $"Envie no máximo {MaxFilesPerMessage} imagens por mensagem.";
            return [];
        }

        var clean = new List<CleanAttachment>(real.Count);
        foreach (var f in real)
        {
            if (f.Length > ImageSanitizer.MaxBytes)
            {
                errors["files"] = "Cada imagem pode ter no máximo 3 MB.";
                return [];
            }

            var image = ImageSanitizer.Clean(f.FileName, await f.ReadAsync(ct).ConfigureAwait(false));
            if (image is null)
            {
                errors["files"] = "Envie imagens PNG, JPG ou BMP (até 3 MB cada).";
                return [];
            }

            clean.Add(image);
        }

        return clean;
    }

    private static string? Text(string? value, int max, int min, string field, Dictionary<string, string> errors)
    {
        var text = value?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim().Normalize(NormalizationForm.FormC);
        if (string.IsNullOrEmpty(text))
        {
            errors[field] = "Campo obrigatório.";
            return null;
        }

        // Remove caracteres de controle, menos quebra de linha e tabulação.
        text = new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray());
        if (text.Length < min)
        {
            errors[field] = $"Escreva pelo menos {min} caracteres.";
        }
        else if (text.Length > max)
        {
            errors[field] = $"Use no máximo {max.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))} caracteres.";
        }

        return text;
    }

    private static bool SameLogin(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private Task Audit(string actorType, string actor, string action, int ticketId, ClientInfo client, object details, CancellationToken ct) =>
        audit.WriteAsync(actorType, actor, action, "ticket", ticketId.ToString(CultureInfo.InvariantCulture), client.IpAddress, JsonSerializer.Serialize(details), ct);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suporte: falha ao avisar o jogador do chamado {TicketId} por e-mail")]
    private static partial void LogNotifyFailed(ILogger logger, int ticketId, Exception ex);
}
