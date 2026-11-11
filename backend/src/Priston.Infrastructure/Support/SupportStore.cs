using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Priston.Application.Support;
using Priston.Infrastructure.Accounts;
using Priston.Infrastructure.Admin;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure.Support;

internal sealed class SupportStore(IPortalDbConnectionFactory db, IOptions<PortalDatabaseOptions> options) : PortalStore(db, options), ISupportStore
{
    private const string TicketColumns = """
        t.Id, t.GameLogin, t.Category, t.Subject, t.Status, t.Priority, t.AssignedStaffId, s.DisplayName AS AssignedName,
        t.CreatedUtc, t.UpdatedUtc, t.LastAuthor, t.PlayerUnread, t.ClosedUtc, t.ClosedBy
        """;

    private const string TicketFrom = "dbo.SupportTicket t LEFT JOIN dbo.StaffUser s ON s.Id = t.AssignedStaffId";

    public async Task<int> CountActiveAsync(string gameLogin, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(Cmd(
            "SELECT COUNT(*) FROM dbo.SupportTicket WHERE GameLogin = @login AND Status IN ('aberto','em_atendimento','aguardando')",
            new { login = Ansi(gameLogin, 32) }, ct)).ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(string gameLogin, string category, string subject, string body, IReadOnlyList<CleanAttachment> files, DateTimeOffset at, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        var when = Utc.From(at);
        var id = await conn.ExecuteScalarAsync<int>(Cmd(
            """
            INSERT INTO dbo.SupportTicket (GameLogin, Category, Subject, Status, CreatedUtc, UpdatedUtc, LastAuthor)
            OUTPUT INSERTED.Id
            VALUES (@login, @category, @subject, 'aberto', @at, @at, 'jogador')
            """,
            new { login = Ansi(gameLogin, 32), category = Ansi(category, 16), subject, at = when }, tx, ct)).ConfigureAwait(false);
        await InsertMessageAsync(conn, tx, id, "jogador", null, body, false, files, when, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return id;
    }

    public async Task<TicketSummary?> GetAsync(int id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var r = await conn.QueryFirstOrDefaultAsync<TicketRow>(Cmd($"SELECT {TicketColumns} FROM {TicketFrom} WHERE t.Id = @id", new { id }, ct)).ConfigureAwait(false);
        return r?.ToModel();
    }

    public async Task<(IReadOnlyList<TicketSummary> Items, int Total)> ListByPlayerAsync(string gameLogin, int skip, int take, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT COUNT(*) FROM dbo.SupportTicket WHERE GameLogin = @login;
            SELECT {TicketColumns} FROM {TicketFrom} WHERE t.GameLogin = @login
            ORDER BY CASE WHEN t.Status IN ('resolvido','fechado') THEN 1 ELSE 0 END, t.UpdatedUtc DESC, t.Id DESC
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new { login = Ansi(gameLogin, 32), skip, take }, ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<TicketRow>().ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<(IReadOnlyList<TicketSummary> Items, int Total)> ListQueueAsync(TicketQueueFilter f, int skip, int take, CancellationToken ct)
    {
        // "ativos" = tudo que não foi resolvido nem fechado. A fila mostra primeiro quem espera há mais tempo.
        const string where = """
            WHERE (@status IS NULL
                   OR (@status = 'ativos' AND t.Status IN ('aberto','em_atendimento','aguardando'))
                   OR t.Status = @status)
              AND (@category IS NULL OR t.Category = @category)
              AND (@assigned IS NULL OR t.AssignedStaffId = @assigned)
              AND (@login IS NULL OR t.GameLogin = @login)
              AND (@waiting = 0 OR (t.LastAuthor = 'jogador' AND t.Status IN ('aberto','em_atendimento')))
            """;
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        using var grid = await conn.QueryMultipleAsync(Cmd(
            $"""
            SELECT COUNT(*) FROM dbo.SupportTicket t {where};
            SELECT {TicketColumns} FROM {TicketFrom} {where}
            ORDER BY CASE t.Priority WHEN 'alta' THEN 0 WHEN 'normal' THEN 1 ELSE 2 END,
                     CASE WHEN t.Status IN ('resolvido','fechado') THEN 1 ELSE 0 END,
                     CASE WHEN t.Status IN ('resolvido','fechado') THEN NULL ELSE t.UpdatedUtc END ASC,
                     t.UpdatedUtc DESC, t.Id
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """,
            new
            {
                status = Ansi(f.Status, 16),
                category = Ansi(f.Category, 16),
                assigned = f.AssignedStaffId,
                login = Ansi(f.GameLogin, 32),
                waiting = f.WaitingStaff,
                skip,
                take,
            }, ct)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var rows = await grid.ReadAsync<TicketRow>().ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<IReadOnlyList<TicketMessage>> ListMessagesAsync(int ticketId, bool includeInternal, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<MessageRow>(Cmd(
            """
            SELECT m.Id, m.AuthorType, m.StaffId, s.DisplayName AS StaffName, m.Body, m.Internal, m.CreatedUtc
            FROM dbo.SupportMessage m LEFT JOIN dbo.StaffUser s ON s.Id = m.StaffId
            WHERE m.TicketId = @ticketId AND (@includeInternal = 1 OR m.Internal = 0)
            ORDER BY m.Id
            """,
            new { ticketId, includeInternal }, ct)).ConfigureAwait(false);
        return rows.Select(r => new TicketMessage(r.Id, r.AuthorType, r.StaffId, r.StaffName, r.Body, r.Internal, Utc.To(r.CreatedUtc))).ToList();
    }

    public async Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(int ticketId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<AttachmentInfo>(Cmd(
            "SELECT Id, MessageId, FileName, ContentType, SizeBytes FROM dbo.SupportAttachment WHERE TicketId = @ticketId ORDER BY CreatedUtc, FileName",
            new { ticketId }, ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<long> AddMessageAsync(int ticketId, string authorType, int? staffId, string body, bool isInternal, IReadOnlyList<CleanAttachment> files,
        TicketTransition? transition, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        var when = Utc.From(transition?.At ?? DateTimeOffset.UtcNow);
        var id = await InsertMessageAsync(conn, tx, ticketId, authorType, staffId, body, isInternal, files, when, ct).ConfigureAwait(false);
        if (transition is not null)
        {
            await conn.ExecuteAsync(Cmd(
                """
                UPDATE dbo.SupportTicket
                   SET Status = @status, LastAuthor = @lastAuthor, PlayerUnread = @unread, UpdatedUtc = @at,
                       ClosedUtc = CASE WHEN @status = 'fechado' THEN COALESCE(ClosedUtc, @at) ELSE NULL END,
                       ClosedBy  = CASE WHEN @status = 'fechado' THEN COALESCE(ClosedBy, @closedBy) ELSE NULL END
                 WHERE Id = @ticketId
                """,
                new
                {
                    ticketId,
                    status = Ansi(transition.Status, 16),
                    lastAuthor = Ansi(transition.LastAuthor, 8),
                    unread = transition.PlayerUnread,
                    at = when,
                    closedBy = Ansi(transition.ClosedBy, 40),
                }, tx, ct)).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return id;
    }

    public async Task MarkPlayerReadAsync(int ticketId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd("UPDATE dbo.SupportTicket SET PlayerUnread = 0 WHERE Id = @ticketId", new { ticketId }, ct)).ConfigureAwait(false);
    }

    public async Task UpdateAsync(int ticketId, string status, string priority, int? assignedStaffId, string? closedBy, DateTimeOffset at, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(Cmd(
            """
            UPDATE dbo.SupportTicket
               SET Status = @status, Priority = @priority, AssignedStaffId = @assigned, UpdatedUtc = @at,
                   ClosedUtc = CASE WHEN @status = 'fechado' THEN COALESCE(ClosedUtc, @at) ELSE NULL END,
                   ClosedBy  = CASE WHEN @status = 'fechado' THEN @closedBy ELSE NULL END
             WHERE Id = @ticketId
            """,
            new { ticketId, status = Ansi(status, 16), priority = Ansi(priority, 8), assigned = assignedStaffId, at = Utc.From(at), closedBy = Ansi(closedBy, 40) },
            ct)).ConfigureAwait(false);
    }

    public async Task<AttachmentContent?> GetAttachmentAsync(int ticketId, Guid id, bool includeInternal, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        return await conn.QueryFirstOrDefaultAsync<AttachmentContent>(Cmd(
            """
            SELECT a.FileName, a.ContentType, a.Data
            FROM dbo.SupportAttachment a JOIN dbo.SupportMessage m ON m.Id = a.MessageId
            WHERE a.Id = @id AND a.TicketId = @ticketId AND (@includeInternal = 1 OR m.Internal = 0)
            """,
            new { id, ticketId, includeInternal }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SupportAgent>> ListAgentsAsync(IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SupportAgent>(Cmd(
            "SELECT Id, Username, DisplayName FROM dbo.StaffUser WHERE Active = 1 AND Role IN @roles ORDER BY DisplayName",
            new { roles = roles.ToArray() }, ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    private async Task<long> InsertMessageAsync(SqlConnection conn, SqlTransaction tx, int ticketId, string authorType, int? staffId, string body, bool isInternal,
        IReadOnlyList<CleanAttachment> files, DateTime at, CancellationToken ct)
    {
        var id = await conn.ExecuteScalarAsync<long>(Cmd(
            """
            INSERT INTO dbo.SupportMessage (TicketId, AuthorType, StaffId, Body, Internal, CreatedUtc)
            OUTPUT INSERTED.Id
            VALUES (@ticketId, @authorType, @staffId, @body, @isInternal, @at)
            """,
            new { ticketId, authorType = Ansi(authorType, 8), staffId, body, isInternal, at }, tx, ct)).ConfigureAwait(false);
        foreach (var f in files)
        {
            await conn.ExecuteAsync(Cmd(
                """
                INSERT INTO dbo.SupportAttachment (Id, TicketId, MessageId, FileName, ContentType, SizeBytes, Data, CreatedUtc)
                VALUES (@attId, @ticketId, @id, @fileName, @contentType, @size, @data, @at)
                """,
                new { attId = Guid.NewGuid(), ticketId, id, fileName = f.FileName, contentType = Ansi(f.ContentType, 16), size = f.Data.Length, data = f.Data, at },
                tx, ct)).ConfigureAwait(false);
        }

        return id;
    }

    private sealed class TicketRow
    {
        public int Id { get; init; }
        public string GameLogin { get; init; } = "";
        public string Category { get; init; } = "";
        public string Subject { get; init; } = "";
        public string Status { get; init; } = "";
        public string Priority { get; init; } = "";
        public int? AssignedStaffId { get; init; }
        public string? AssignedName { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
        public string LastAuthor { get; init; } = "";
        public bool PlayerUnread { get; init; }
        public DateTime? ClosedUtc { get; init; }
        public string? ClosedBy { get; init; }

        public TicketSummary ToModel() => new(Id, GameLogin, Category, Subject, Status, Priority, AssignedStaffId, AssignedName,
            Utc.To(CreatedUtc), Utc.To(UpdatedUtc), LastAuthor, PlayerUnread, Utc.To(ClosedUtc), ClosedBy);
    }

    private sealed class MessageRow
    {
        public long Id { get; init; }
        public string AuthorType { get; init; } = "";
        public int? StaffId { get; init; }
        public string? StaffName { get; init; }
        public string Body { get; init; } = "";
        public bool Internal { get; init; }
        public DateTime CreatedUtc { get; init; }
    }
}
