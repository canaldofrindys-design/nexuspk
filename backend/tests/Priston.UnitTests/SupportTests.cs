using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Home;
using Priston.Application.Support;

namespace Priston.UnitTests;

internal sealed class MemorySupport : ISupportStore
{
    private int _nextTicket = 1000;
    private long _nextMessage = 1;

    public List<TicketSummary> Tickets { get; } = [];
    public List<TicketMessage> Messages { get; } = [];
    public List<(int TicketId, AttachmentInfo Info, byte[] Data)> Files { get; } = [];
    public Dictionary<long, int> MessageTicket { get; } = [];
    public List<SupportAgent> Agents { get; } = [new(1, "ana", "Ana (Suporte)")];

    public Task<int> CountActiveAsync(string gameLogin, CancellationToken ct) =>
        Task.FromResult(Tickets.Count(t => t.GameLogin == gameLogin && TicketStatuses.IsActive(t.Status)));

    public async Task<int> CreateAsync(string gameLogin, string category, string subject, string body, IReadOnlyList<CleanAttachment> files, DateTimeOffset at, CancellationToken ct)
    {
        var id = _nextTicket++;
        Tickets.Add(new TicketSummary(id, gameLogin, category, subject, TicketStatuses.Open, "normal", null, null, at, at, "jogador", false, null, null));
        await AddMessageAsync(id, "jogador", null, body, false, files, null, ct);
        return id;
    }

    public Task<TicketSummary?> GetAsync(int id, CancellationToken ct) => Task.FromResult(Tickets.FirstOrDefault(t => t.Id == id));

    public Task<(IReadOnlyList<TicketSummary> Items, int Total)> ListByPlayerAsync(string gameLogin, int skip, int take, CancellationToken ct)
    {
        var mine = Tickets.Where(t => t.GameLogin == gameLogin).ToList();
        return Task.FromResult<(IReadOnlyList<TicketSummary>, int)>((mine.Skip(skip).Take(take).ToList(), mine.Count));
    }

    public Task<(IReadOnlyList<TicketSummary> Items, int Total)> ListQueueAsync(TicketQueueFilter filter, int skip, int take, CancellationToken ct) =>
        Task.FromResult<(IReadOnlyList<TicketSummary>, int)>((Tickets.ToList(), Tickets.Count));

    public Task<IReadOnlyList<TicketMessage>> ListMessagesAsync(int ticketId, bool includeInternal, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TicketMessage>>(Messages.Where(m => MessageTicket[m.Id] == ticketId && (includeInternal || !m.Internal)).ToList());

    public Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(int ticketId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AttachmentInfo>>(Files.Where(f => f.TicketId == ticketId).Select(f => f.Info).ToList());

    public Task<long> AddMessageAsync(int ticketId, string authorType, int? staffId, string body, bool isInternal, IReadOnlyList<CleanAttachment> files,
        TicketTransition? transition, CancellationToken ct)
    {
        var id = _nextMessage++;
        MessageTicket[id] = ticketId;
        Messages.Add(new TicketMessage(id, authorType, staffId, staffId is null ? null : "Ana (Suporte)", body, isInternal, transition?.At ?? DateTimeOffset.UnixEpoch));
        foreach (var f in files)
        {
            Files.Add((ticketId, new AttachmentInfo(Guid.NewGuid(), id, f.FileName, f.ContentType, f.Data.Length), f.Data));
        }

        if (transition is not null)
        {
            Replace(ticketId, t => t with
            {
                Status = transition.Status,
                LastAuthor = transition.LastAuthor,
                PlayerUnread = transition.PlayerUnread,
                UpdatedUtc = transition.At,
                ClosedBy = transition.Status == TicketStatuses.Closed ? t.ClosedBy ?? transition.ClosedBy : null,
            });
        }

        return Task.FromResult(id);
    }

    public Task MarkPlayerReadAsync(int ticketId, CancellationToken ct)
    {
        Replace(ticketId, t => t with { PlayerUnread = false });
        return Task.CompletedTask;
    }

    public Task UpdateAsync(int ticketId, string status, string priority, int? assignedStaffId, string? closedBy, DateTimeOffset at, CancellationToken ct)
    {
        Replace(ticketId, t => t with { Status = status, Priority = priority, AssignedStaffId = assignedStaffId, ClosedBy = closedBy, UpdatedUtc = at });
        return Task.CompletedTask;
    }

    public Task<AttachmentContent?> GetAttachmentAsync(int ticketId, Guid id, bool includeInternal, CancellationToken ct)
    {
        var f = Files.FirstOrDefault(x => x.TicketId == ticketId && x.Info.Id == id);
        if (f.Info is null || (!includeInternal && Messages.First(m => m.Id == f.Info.MessageId).Internal))
        {
            return Task.FromResult<AttachmentContent?>(null);
        }

        return Task.FromResult<AttachmentContent?>(new AttachmentContent(f.Info.FileName, f.Info.ContentType, f.Data));
    }

    public Task<IReadOnlyList<SupportAgent>> ListAgentsAsync(IReadOnlyCollection<string> roles, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SupportAgent>>(Agents);

    private void Replace(int id, Func<TicketSummary, TicketSummary> change)
    {
        var i = Tickets.FindIndex(t => t.Id == id);
        Tickets[i] = change(Tickets[i]);
    }
}

public class ImageSanitizerTests
{
    internal static byte[] Png(bool withText = false, bool trailing = false, int width = 2)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), 2);
        ihdr[8] = 8;
        ihdr[9] = 2;
        Chunk(ms, "IHDR", ihdr);
        if (withText)
        {
            Chunk(ms, "tEXt", Encoding.ASCII.GetBytes("Author\0Fulano de Tal"));
        }

        Chunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        Chunk(ms, "IEND", []);
        if (trailing)
        {
            ms.Write(Encoding.ASCII.GetBytes("<script>alert(1)</script>"));
        }

        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);
        s.Write(Encoding.ASCII.GetBytes(type));
        s.Write(data);
        s.Write(new byte[4]); // CRC não é conferido
    }

    internal static byte[] Jpeg(bool withExif)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xD8]);
        ms.Write([0xFF, 0xE0, 0x00, 0x10, .. Encoding.ASCII.GetBytes("JFIF\0"), 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        if (withExif)
        {
            var exif = Encoding.ASCII.GetBytes("Exif\0\0GPS-22.9,-43.2");
            ms.Write([0xFF, 0xE1, (byte)((exif.Length + 2) >> 8), (byte)(exif.Length + 2), .. exif]);
            var comment = Encoding.ASCII.GetBytes("comentario");
            ms.Write([0xFF, 0xFE, 0x00, (byte)(comment.Length + 2), .. comment]);
        }

        ms.Write([0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x02, 0x00, 0x02, 0x01, 0x01, 0x11, 0x00]);
        ms.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x12, 0x34, 0xFF, 0xD9]);
        return ms.ToArray();
    }

    [Fact]
    public void Png_text_chunks_and_trailing_bytes_are_removed()
    {
        var clean = ImageSanitizer.Clean("foto.PNG", Png(withText: true, trailing: true));
        Assert.NotNull(clean);
        Assert.Equal("image/png", clean.ContentType);
        Assert.Equal("foto.png", clean.FileName);
        Assert.Equal(Png(), clean.Data);
    }

    [Fact]
    public void Jpeg_exif_and_comments_are_removed()
    {
        var clean = ImageSanitizer.Clean("C:\\Users\\fulano\\print.jpeg", Jpeg(withExif: true));
        Assert.NotNull(clean);
        Assert.Equal("image/jpeg", clean.ContentType);
        Assert.Equal("print.jpg", clean.FileName);
        Assert.Equal(Jpeg(withExif: false), clean.Data);
        Assert.DoesNotContain("GPS", Encoding.ASCII.GetString(clean.Data), StringComparison.Ordinal);
    }

    [Fact]
    public void Bmp_is_cut_at_declared_size()
    {
        var bmp = new byte[70];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(2), 62);
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(10), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), -2);
        var clean = ImageSanitizer.Clean("tela.bmp", bmp);
        Assert.NotNull(clean);
        Assert.Equal(62, clean.Data.Length);
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("GIF89a.......................")]
    [InlineData("%PDF-1.7 ........................")]
    public void Non_images_are_rejected(string content) =>
        Assert.Null(ImageSanitizer.Clean("imagem.png", Encoding.ASCII.GetBytes(content)));

    [Fact]
    public void Truncated_or_oversized_images_are_rejected()
    {
        var png = Png();
        Assert.Null(ImageSanitizer.Clean("a.png", png[..^14]));
        Assert.Null(ImageSanitizer.Clean("a.png", Png(width: 20_000)));
        Assert.Null(ImageSanitizer.Clean("a.jpg", Jpeg(false)[..^2]));
        Assert.Null(ImageSanitizer.Clean("a.png", new byte[ImageSanitizer.MaxBytes + 1]));
    }

    [Theory]
    [InlineData("../../etc/passwd.png", "image/png", "passwd.png")]
    [InlineData("<img src=x>.jpg", "image/jpeg", "img srcx.jpg")]
    [InlineData("....", "image/bmp", "imagem.bmp")]
    public void File_names_are_sanitized(string input, string type, string expected) =>
        Assert.Equal(expected, ImageSanitizer.SafeName(input, type));
}

public class SupportServiceTests
{
    private static readonly ClientInfo Client = new("198.51.100.7", "teste");
    private readonly MemorySupport _store = new();
    private readonly MemoryAccounts _accounts = new();
    private readonly CapturingEmail _email = new();
    private readonly MemoryAudit _audit = new();
    private readonly FixedClock _clock = new(ContentFixture.Now);

    private SupportService Service() => new(_store, _accounts, _email, _audit, _clock,
        Options.Create(new ServerPresentationOptions { Name = "NexusPK" }),
        Options.Create(new AccountOptions { PublicBaseUrl = "https://portal.example.test" }),
        NullLogger<SupportService>.Instance);

    private static StaffPrincipal Staff => new(1, "ana", "Ana (Suporte)", StaffRoles.Support, false, ContentFixture.Now.AddHours(1));

    private static UploadedFile File(string name, byte[] data) => new(name, data.Length, _ => Task.FromResult(data));

    private static readonly NewTicketRequest Valid = new("conta", "Não consigo entrar", "Desde ontem o jogo diz que a senha está errada.");

    [Fact]
    public async Task Player_opens_ticket_with_clean_image_and_audit()
    {
        var r = await Service().OpenAsync("foxtrot01", Valid, [File("print.png", ImageSanitizerTests.Png(withText: true))], Client, default);
        Assert.Equal(SupportStatus.Ok, r.Status);
        Assert.Single(_store.Files);
        Assert.Equal(ImageSanitizerTests.Png(), _store.Files[0].Data);
        var audit = Assert.Single(_audit.Rows);
        Assert.Equal("ticket.created", audit.Action);
        Assert.DoesNotContain("senha", audit.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_input_returns_field_errors()
    {
        var r = await Service().OpenAsync("foxtrot01", new("inexistente", "oi", ""), [File("x.png", Encoding.ASCII.GetBytes("não é imagem"))], Client, default);
        Assert.Equal(SupportStatus.ValidationFailed, r.Status);
        Assert.Contains("category", r.Errors!.Keys);
        Assert.Contains("subject", r.Errors.Keys);
        Assert.Contains("body", r.Errors.Keys);
        Assert.Contains("files", r.Errors.Keys);
        Assert.Empty(_store.Tickets);
    }

    [Fact]
    public async Task Too_many_files_are_rejected()
    {
        var png = ImageSanitizerTests.Png();
        var r = await Service().OpenAsync("foxtrot01", Valid, [File("1.png", png), File("2.png", png), File("3.png", png), File("4.png", png)], Client, default);
        Assert.Equal("Envie no máximo 3 imagens por mensagem.", r.Errors!["files"]);
    }

    [Fact]
    public async Task Limit_of_active_tickets_per_account()
    {
        var s = Service();
        for (var i = 0; i < SupportService.MaxActiveTicketsPerPlayer; i++)
        {
            Assert.Equal(SupportStatus.Ok, (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).Status);
        }

        Assert.Equal(SupportStatus.TooManyOpen, (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).Status);
        await s.PlayerCloseAsync("foxtrot01", 1000, Client, default);
        Assert.Equal(SupportStatus.Ok, (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).Status);
    }

    [Fact]
    public async Task Player_cannot_see_or_touch_other_accounts_tickets()
    {
        var s = Service();
        var id = (await s.OpenAsync("foxtrot01", Valid, [File("a.png", ImageSanitizerTests.Png())], Client, default)).TicketId!.Value;
        Assert.Null(await s.GetMineAsync("outraconta", id, default));
        Assert.Equal(SupportStatus.NotFound, (await s.PlayerReplyAsync("outraconta", id, "oi, tudo bem?", [], Client, default)).Status);
        Assert.Equal(SupportStatus.NotFound, (await s.PlayerCloseAsync("outraconta", id, Client, default)).Status);
        Assert.Null(await s.GetMyAttachmentAsync("outraconta", id, _store.Files[0].Info.Id, default));
        Assert.NotNull(await s.GetMyAttachmentAsync("FOXTROT01", id, _store.Files[0].Info.Id, default));
    }

    [Fact]
    public async Task Internal_notes_and_their_files_never_reach_the_player()
    {
        var s = Service();
        var id = (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).TicketId!.Value;
        await s.StaffReplyAsync(Staff, id, new("Conferir IP no log antes de responder.", true, null), [File("log.png", ImageSanitizerTests.Png())], Client, default);

        var mine = await s.GetMineAsync("foxtrot01", id, default);
        Assert.Single(mine!.Messages);
        Assert.Null(await s.GetMyAttachmentAsync("foxtrot01", id, _store.Files[0].Info.Id, default));
        Assert.Equal(2, (await s.GetForStaffAsync(id, default))!.Messages.Count);
        Assert.Equal(TicketStatuses.Open, _store.Tickets[0].Status);
        Assert.False(_store.Tickets[0].PlayerUnread);
        Assert.Empty(_email.Sent);
        Assert.Equal("ticket.note", _audit.Rows[^1].Action);
    }

    [Fact]
    public async Task Staff_reply_waits_for_player_and_emails_confirmed_address_without_the_text()
    {
        _accounts.Rows["foxtrot01"] = new PlayerAccount("foxtrot01", "foxtrot@example.test", ContentFixture.Now.AddDays(-1), ContentFixture.Now.AddDays(-10), null);
        var s = Service();
        var id = (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).TicketId!.Value;
        await s.StaffReplyAsync(Staff, id, new("Sua conta foi desbloqueada, tente de novo.", false, null), [], Client, default);

        Assert.Equal(TicketStatuses.WaitingPlayer, _store.Tickets[0].Status);
        Assert.True(_store.Tickets[0].PlayerUnread);
        var mail = Assert.Single(_email.Sent);
        Assert.Equal("foxtrot@example.test", mail.To);
        Assert.Contains("/painel/suporte/1000", mail.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("desbloqueada", mail.TextBody, StringComparison.Ordinal);

        // Abrir o chamado marca como lido; responder devolve para a fila.
        var mine = await s.GetMineAsync("foxtrot01", id, default);
        Assert.Equal("Ana (Suporte)", mine!.Messages[1].StaffName);
        Assert.False(_store.Tickets[0].PlayerUnread);
        await s.PlayerReplyAsync("foxtrot01", id, "Funcionou, obrigado!", [], Client, default);
        Assert.Equal(TicketStatuses.Open, _store.Tickets[0].Status);
        Assert.Equal("jogador", _store.Tickets[0].LastAuthor);
    }

    [Fact]
    public async Task Unconfirmed_email_gets_no_notification()
    {
        _accounts.Rows["foxtrot01"] = new PlayerAccount("foxtrot01", "foxtrot@example.test", null, ContentFixture.Now.AddDays(-10), null);
        var s = Service();
        var id = (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).TicketId!.Value;
        await s.StaffReplyAsync(Staff, id, new("Resposta da equipe.", false, null), [], Client, default);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Closed_ticket_rejects_player_reply_but_resolved_reopens()
    {
        var s = Service();
        var id = (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).TicketId!.Value;
        await s.StaffReplyAsync(Staff, id, new("Resolvido do nosso lado.", false, TicketStatuses.Resolved), [], Client, default);
        Assert.Equal(TicketStatuses.Resolved, _store.Tickets[0].Status);
        await s.PlayerReplyAsync("foxtrot01", id, "Voltou a acontecer.", [], Client, default);
        Assert.Equal(TicketStatuses.Open, _store.Tickets[0].Status);

        await s.PlayerCloseAsync("foxtrot01", id, Client, default);
        Assert.Equal(TicketStatuses.Closed, _store.Tickets[0].Status);
        Assert.Equal("jogador", _store.Tickets[0].ClosedBy);
        Assert.Equal(SupportStatus.Closed, (await s.PlayerReplyAsync("foxtrot01", id, "mais uma coisa", [], Client, default)).Status);
    }

    [Fact]
    public async Task Staff_update_validates_assignee_and_audits_change()
    {
        var s = Service();
        var id = (await s.OpenAsync("foxtrot01", Valid, [], Client, default)).TicketId!.Value;
        var bad = await s.UpdateAsync(Staff, id, new(TicketStatuses.InProgress, "urgente", 99), Client, default);
        Assert.Equal(SupportStatus.ValidationFailed, bad.Status);
        Assert.Contains("priority", bad.Errors!.Keys);
        Assert.Contains("assignedStaffId", bad.Errors.Keys);

        var ok = await s.UpdateAsync(Staff, id, new(TicketStatuses.InProgress, "alta", 1), Client, default);
        Assert.Equal(SupportStatus.Ok, ok.Status);
        Assert.Equal(1, _store.Tickets[0].AssignedStaffId);
        Assert.Equal("alta", _store.Tickets[0].Priority);
        Assert.Equal("ticket.updated", _audit.Rows[^1].Action);
    }

    [Fact]
    public void Support_capability_by_role()
    {
        Assert.True(StaffRoles.Can(StaffRoles.Support, Capability.ManageSupport));
        Assert.True(StaffRoles.Can(StaffRoles.Moderator, Capability.ManageSupport));
        Assert.False(StaffRoles.Can(StaffRoles.Support, Capability.ManageContent));
        Assert.False(StaffRoles.Can(StaffRoles.Support, Capability.ViewAudit));
    }
}
