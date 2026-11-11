namespace Priston.Application.Support;

public static class TicketCategories
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["conta"] = "Conta e acesso",
        ["personagem"] = "Personagem e itens",
        ["bug"] = "Erro no jogo",
        ["denuncia"] = "Denúncia",
        ["evento"] = "Eventos",
        ["loja"] = "Loja e coins",
        ["outro"] = "Outro assunto",
    };

    public static bool IsValid(string? value) => value is not null && Labels.ContainsKey(value);
}

public static class TicketStatuses
{
    public const string Open = "aberto";
    public const string InProgress = "em_atendimento";
    public const string WaitingPlayer = "aguardando";
    public const string Resolved = "resolvido";
    public const string Closed = "fechado";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Open] = "Aberto",
        [InProgress] = "Em atendimento",
        [WaitingPlayer] = "Aguardando jogador",
        [Resolved] = "Resolvido",
        [Closed] = "Fechado",
    };

    public static bool IsValid(string? value) => value is not null && Labels.ContainsKey(value);

    /// <summary>Chamados que ainda contam no limite de chamados abertos por conta.</summary>
    public static bool IsActive(string status) => status is Open or InProgress or WaitingPlayer;
}

public static class TicketPriorities
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["baixa"] = "Baixa",
        ["normal"] = "Normal",
        ["alta"] = "Alta",
    };

    public static bool IsValid(string? value) => value is not null && Labels.ContainsKey(value);
}

public enum SupportStatus
{
    Ok,
    ValidationFailed,
    NotFound,
    /// <summary>Chamado fechado: o jogador abre outro.</summary>
    Closed,
    TooManyOpen,
}

/// <summary>Arquivo recebido do formulário, antes da validação.</summary>
public sealed record UploadedFile(string FileName, long Length, Func<CancellationToken, Task<byte[]>> ReadAsync);

/// <summary>Imagem validada e sem metadados, pronta para gravar.</summary>
public sealed record CleanAttachment(string FileName, string ContentType, byte[] Data);

public sealed record AttachmentInfo(Guid Id, long MessageId, string FileName, string ContentType, int SizeBytes);

public sealed record AttachmentContent(string FileName, string ContentType, byte[] Data);

public sealed record TicketSummary(
    int Id,
    string GameLogin,
    string Category,
    string Subject,
    string Status,
    string Priority,
    int? AssignedStaffId,
    string? AssignedName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string LastAuthor,
    bool PlayerUnread,
    DateTimeOffset? ClosedUtc,
    string? ClosedBy);

public sealed record TicketMessage(long Id, string AuthorType, int? StaffId, string? StaffName, string Body, bool Internal, DateTimeOffset CreatedUtc);

public sealed record TicketMessageView(long Id, string AuthorType, string? StaffName, string Body, bool Internal, DateTimeOffset CreatedUtc, IReadOnlyList<AttachmentInfo> Attachments);

public sealed record TicketDetail(TicketSummary Ticket, IReadOnlyList<TicketMessageView> Messages);

public sealed record TicketQueueFilter(string? Status, string? Category, int? AssignedStaffId, string? GameLogin, bool WaitingStaff);

public sealed record TicketList(IReadOnlyList<TicketSummary> Items, int Total, int Page, int PageSize);

public sealed record SupportAgent(int Id, string Username, string DisplayName);

public sealed record NewTicketRequest(string? Category, string? Subject, string? Body);

public sealed record StaffReplyRequest(string? Body, bool Internal, string? StatusAfter);

public sealed record TicketUpdateRequest(string? Status, string? Priority, int? AssignedStaffId);

public sealed record SupportResult(SupportStatus Status, int? TicketId = null, IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>Mudança de estado gravada junto com uma mensagem nova.</summary>
public sealed record TicketTransition(string Status, string LastAuthor, bool PlayerUnread, DateTimeOffset At, string? ClosedBy);

public interface ISupportStore
{
    Task<int> CountActiveAsync(string gameLogin, CancellationToken ct);

    /// <summary>Cria chamado, primeira mensagem e anexos numa transação.</summary>
    Task<int> CreateAsync(string gameLogin, string category, string subject, string body, IReadOnlyList<CleanAttachment> files, DateTimeOffset at, CancellationToken ct);

    Task<TicketSummary?> GetAsync(int id, CancellationToken ct);

    Task<(IReadOnlyList<TicketSummary> Items, int Total)> ListByPlayerAsync(string gameLogin, int skip, int take, CancellationToken ct);

    Task<(IReadOnlyList<TicketSummary> Items, int Total)> ListQueueAsync(TicketQueueFilter filter, int skip, int take, CancellationToken ct);

    Task<IReadOnlyList<TicketMessage>> ListMessagesAsync(int ticketId, bool includeInternal, CancellationToken ct);

    Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(int ticketId, CancellationToken ct);

    /// <summary>Grava a mensagem, os anexos e a nova situação numa transação. Devolve o Id da mensagem.</summary>
    Task<long> AddMessageAsync(int ticketId, string authorType, int? staffId, string body, bool isInternal, IReadOnlyList<CleanAttachment> files,
        TicketTransition? transition, CancellationToken ct);

    Task MarkPlayerReadAsync(int ticketId, CancellationToken ct);

    Task UpdateAsync(int ticketId, string status, string priority, int? assignedStaffId, string? closedBy, DateTimeOffset at, CancellationToken ct);

    /// <summary>Anexo, se pertence ao chamado. Com <paramref name="includeInternal"/> false, anexos de notas internas não voltam.</summary>
    Task<AttachmentContent?> GetAttachmentAsync(int ticketId, Guid id, bool includeInternal, CancellationToken ct);

    Task<IReadOnlyList<SupportAgent>> ListAgentsAsync(IReadOnlyCollection<string> roles, CancellationToken ct);
}
