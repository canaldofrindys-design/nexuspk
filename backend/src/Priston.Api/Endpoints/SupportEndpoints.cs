using Microsoft.AspNetCore.Mvc;
using Priston.Application.Accounts;
using Priston.Application.Admin;
using Priston.Application.Server;
using Priston.Application.Support;

namespace Priston.Api.Endpoints;

/// <summary>
/// Central de suporte (jogador e equipe) e página de status do servidor.
/// Abrir e responder chamado usa multipart/form-data (texto + até 3 imagens).
/// </summary>
public static class SupportEndpoints
{
    /// <summary>3 imagens de 3 MB e o texto, com folga para o envelope multipart.</summary>
    private const long MaxFormBytes = 11L * 1024 * 1024;

    public static void MapSupportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/status", async (StatusPageService status, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await status.GetAsync(ct));
        });

        app.MapGet("/api/v1/support/options", () => Results.Ok(new
        {
            Categories = TicketCategories.Labels.Select(kv => new { Value = kv.Key, Label = kv.Value }),
            Statuses = TicketStatuses.Labels.Select(kv => new { Value = kv.Key, Label = kv.Value }),
            Priorities = TicketPriorities.Labels.Select(kv => new { Value = kv.Key, Label = kv.Value }),
            SupportService.MaxActiveTicketsPerPlayer,
            SupportService.MaxFilesPerMessage,
            MaxFileBytes = ImageSanitizer.MaxBytes,
            SupportService.BodyMaxLength,
            SupportService.SubjectMaxLength,
        }));

        MapPlayer(app.MapGroup("/api/v1/player/tickets"));
        MapStaff(app.MapGroup("/api/v1/admin"));
    }

    private static void MapPlayer(RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });

        group.MapGet("", async (AuthService auth, SupportService support, HttpContext http, int? pagina, CancellationToken ct) =>
            await AsPlayer(auth, http, async me => Results.Ok(await support.ListMineAsync(me.Login, pagina ?? 1, ct)), ct));

        group.MapPost("", async (AuthService auth, SupportService support, HttpContext http, CancellationToken ct) =>
            await AsPlayer(auth, http, async me =>
            {
                var form = await ReadFormAsync(http, ct);
                if (form is null)
                {
                    return BadForm();
                }

                var request = new NewTicketRequest(form["category"], form["subject"], form["body"]);
                var r = await support.OpenAsync(me.Login, request, Files(form), AuthEndpoints.Client(http), ct);
                return r.Status == SupportStatus.Ok ? Results.Created($"/api/v1/player/tickets/{r.TicketId}", new { id = r.TicketId }) : Error(r);
            }, ct))
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxFormBytes));

        group.MapGet("/{id:int}", async (AuthService auth, SupportService support, HttpContext http, int id, CancellationToken ct) =>
            await AsPlayer(auth, http, async me => await support.GetMineAsync(me.Login, id, ct) is { } t ? Results.Ok(PlayerView(t)) : Results.NotFound(), ct));

        group.MapPost("/{id:int}/messages", async (AuthService auth, SupportService support, HttpContext http, int id, CancellationToken ct) =>
            await AsPlayer(auth, http, async me =>
            {
                var form = await ReadFormAsync(http, ct);
                if (form is null)
                {
                    return BadForm();
                }

                var r = await support.PlayerReplyAsync(me.Login, id, form["body"], Files(form), AuthEndpoints.Client(http), ct);
                return r.Status == SupportStatus.Ok ? Results.NoContent() : Error(r);
            }, ct))
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxFormBytes));

        group.MapPost("/{id:int}/close", async (AuthService auth, SupportService support, HttpContext http, int id, CancellationToken ct) =>
            await AsPlayer(auth, http, async me =>
            {
                var r = await support.PlayerCloseAsync(me.Login, id, AuthEndpoints.Client(http), ct);
                return r.Status == SupportStatus.Ok ? Results.NoContent() : Error(r);
            }, ct));

        group.MapGet("/{id:int}/attachments/{attachmentId:guid}", async (AuthService auth, SupportService support, HttpContext http, int id, Guid attachmentId, CancellationToken ct) =>
            await AsPlayer(auth, http, async me => File(http, await support.GetMyAttachmentAsync(me.Login, id, attachmentId, ct)), ct));
    }

    private static void MapStaff(RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });

        group.MapGet("/tickets", async (StaffAuthService auth, SupportService support, HttpContext http,
            string? situacao, string? categoria, int? atendente, string? login, bool? aguardandoEquipe, int? pagina, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSupport, async _ =>
                Results.Ok(await support.QueueAsync(new TicketQueueFilter(situacao, categoria, atendente, login, aguardandoEquipe ?? false), pagina ?? 1, ct)), ct));

        group.MapGet("/tickets/agents", async (StaffAuthService auth, SupportService support, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSupport, async _ => Results.Ok(await support.AgentsAsync(ct)), ct));

        group.MapGet("/tickets/{id:int}", async (StaffAuthService auth, SupportService support, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSupport, async _ =>
                await support.GetForStaffAsync(id, ct) is { } t ? Results.Ok(t) : Results.NotFound(), ct));

        group.MapPost("/tickets/{id:int}/messages", async (StaffAuthService auth, SupportService support, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSupport, async me =>
            {
                var form = await ReadFormAsync(http, ct);
                if (form is null)
                {
                    return BadForm();
                }

                var request = new StaffReplyRequest(form["body"], form["internal"] == "true", form["statusAfter"]);
                var r = await support.StaffReplyAsync(me, id, request, Files(form), AuthEndpoints.Client(http), ct);
                return r.Status == SupportStatus.Ok ? Results.NoContent() : Error(r);
            }, ct))
            .WithMetadata(new RequestSizeLimitAttribute(MaxFormBytes));

        group.MapPut("/tickets/{id:int}", async ([FromBody] TicketUpdateRequest body, StaffAuthService auth, SupportService support, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSupport, async me =>
            {
                var r = await support.UpdateAsync(me, id, body, AuthEndpoints.Client(http), ct);
                return r.Status == SupportStatus.Ok ? Results.NoContent() : Error(r);
            }, ct));

        group.MapGet("/tickets/{id:int}/attachments/{attachmentId:guid}", async (StaffAuthService auth, SupportService support, HttpContext http, int id, Guid attachmentId, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSupport, async _ => File(http, await support.GetAttachmentForStaffAsync(id, attachmentId, ct)), ct));

        // Avisos e manutenções da página de status (mesma permissão de notícias e eventos).
        group.MapGet("/notices", async (StaffAuthService auth, ServerNoticeAdminService notices, HttpContext http, int? pagina, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await notices.ListAsync(pagina ?? 1, ct)), ct));

        group.MapGet("/notices/{id:int}", async (StaffAuthService auth, ServerNoticeAdminService notices, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => await notices.GetAsync(id, ct) is { } n ? Results.Ok(n) : Results.NotFound(), ct));

        group.MapPost("/notices", async ([FromBody] NoticeInput body, StaffAuthService auth, ServerNoticeAdminService notices, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await notices.SaveAsync(me, null, body, AuthEndpoints.Client(http), ct)), ct));

        group.MapPut("/notices/{id:int}", async ([FromBody] NoticeInput body, StaffAuthService auth, ServerNoticeAdminService notices, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await notices.SaveAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct));
    }

    /// <summary>O jogador não recebe o Id interno do atendente, só o nome de exibição.</summary>
    private static object PlayerView(TicketDetail t) => new
    {
        Ticket = new
        {
            t.Ticket.Id,
            t.Ticket.Category,
            t.Ticket.Subject,
            t.Ticket.Status,
            t.Ticket.CreatedUtc,
            t.Ticket.UpdatedUtc,
            t.Ticket.ClosedUtc,
            ClosedByPlayer = t.Ticket.ClosedBy == "jogador",
        },
        Messages = t.Messages.Select(m => new { m.Id, m.AuthorType, m.StaffName, m.Body, m.CreatedUtc, m.Attachments }),
    };

    private static async Task<IResult> AsPlayer(AuthService auth, HttpContext http, Func<CurrentPlayer, Task<IResult>> action, CancellationToken ct)
    {
        var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
        return me is null
            ? Results.Json(new AuthEndpoints.ErrorBody("unauthenticated", "Sessão expirada. Entre novamente."), statusCode: StatusCodes.Status401Unauthorized)
            : await action(me);
    }

    private static async Task<IFormCollection?> ReadFormAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasFormContentType)
        {
            return null;
        }

        try
        {
            return await http.Request.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (BadHttpRequestException)
        {
            return null;
        }
    }

    private static List<UploadedFile> Files(IFormCollection form) =>
        form.Files.GetFiles("files").Select(f => new UploadedFile(f.FileName, f.Length, async ct =>
        {
            using var ms = new MemoryStream((int)Math.Min(f.Length, ImageSanitizer.MaxBytes));
            await using var s = f.OpenReadStream();
            await s.CopyToAsync(ms, ct);
            return ms.ToArray();
        })).ToList();

    private static IResult File(HttpContext http, AttachmentContent? a)
    {
        if (a is null)
        {
            return Results.NotFound();
        }

        // Só imagem, nunca interpretada como página; o nome vai codificado no Content-Disposition.
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        http.Response.Headers.CacheControl = "private, max-age=300";
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(a.FileName);
        http.Response.Headers.ContentDisposition = disposition.ToString();
        return Results.File(a.Data, a.ContentType);
    }

    private static IResult BadForm() =>
        Results.Json(new AuthEndpoints.ErrorBody("validation", "Envie o formulário como multipart/form-data."), statusCode: StatusCodes.Status400BadRequest);

    private static IResult Error(SupportResult r) => r.Status switch
    {
        SupportStatus.NotFound => Results.NotFound(),
        SupportStatus.Closed => Results.Json(new AuthEndpoints.ErrorBody("ticket-closed", "Este chamado está fechado. Abra um novo chamado se precisar."), statusCode: StatusCodes.Status409Conflict),
        SupportStatus.TooManyOpen => Results.Json(
            new AuthEndpoints.ErrorBody("too-many-open", $"Você já tem {SupportService.MaxActiveTicketsPerPlayer} chamados em andamento. Responda ou encerre um deles antes de abrir outro."),
            statusCode: StatusCodes.Status409Conflict),
        _ => Results.Json(new AuthEndpoints.ErrorBody("validation", "Confira os campos destacados.", r.Errors), statusCode: StatusCodes.Status400BadRequest),
    };
}
