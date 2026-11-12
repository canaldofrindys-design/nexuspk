using Microsoft.AspNetCore.Mvc;
using Priston.Application.Admin;
using Priston.Application.Site;
using Priston.Application.Support;

namespace Priston.Api.Endpoints;

/// <summary>
/// Conteúdo institucional do site (O Servidor, Download, redes sociais), banners da home e imagens enviadas pela equipe.
/// Leitura pública; escrita só pelo painel.
/// </summary>
public static class SiteEndpoints
{
    private const long MaxUploadBytes = 4L * 1024 * 1024;

    public static void MapSiteEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/site", async (SiteContentService site, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok(await site.GetAsync(ct));
        });

        app.MapGet("/api/v1/media/{id:guid}", async (SiteContentService site, HttpContext http, Guid id, CancellationToken ct) =>
        {
            var file = await site.GetMediaAsync(id, ct);
            if (file is null)
            {
                return Results.NotFound();
            }

            // O Id é aleatório e o arquivo nunca muda: pode ficar no cache por um ano.
            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Data, file.ContentType);
        });

        var admin = app.MapGroup("/api/v1/admin");
        admin.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });

        // Textos do site (administrador e superadministrador)
        admin.MapGet("/site/server", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSite, async _ => Results.Ok(await site.GetServerSettingsAsync(ct)), ct));

        admin.MapPut("/site/server", async ([FromBody] ServerSettingsInput body, StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSite, async me => AdminEndpoints.Saved(await site.SaveServerSettingsAsync(me, body, AuthEndpoints.Client(http), ct)), ct));

        admin.MapGet("/site/downloads", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSite, async _ => Results.Ok(await site.GetDownloadSettingsAsync(ct)), ct));

        admin.MapPut("/site/downloads", async ([FromBody] DownloadSettingsInput body, StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageSite, async me => AdminEndpoints.Saved(await site.SaveDownloadSettingsAsync(me, body, AuthEndpoints.Client(http), ct)), ct));

        // Banners (mesma permissão de notícias e eventos)
        admin.MapGet("/banners", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await site.ListBannersAsync(ct)), ct));

        admin.MapGet("/banners/{id:int}", async (StaffAuthService auth, SiteContentService site, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => await site.GetBannerAsync(id, ct) is { } b ? Results.Ok(b) : Results.NotFound(), ct));

        admin.MapPost("/banners", async ([FromBody] BannerInput body, StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await site.SaveBannerAsync(me, null, body, AuthEndpoints.Client(http), ct)), ct));

        admin.MapPut("/banners/{id:int}", async ([FromBody] BannerInput body, StaffAuthService auth, SiteContentService site, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await site.SaveBannerAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct));

        admin.MapDelete("/banners/{id:int}", async (StaffAuthService auth, SiteContentService site, HttpContext http, int id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await site.DeleteBannerAsync(me, id, AuthEndpoints.Client(http), ct)), ct));

        // Envio de imagem (notícias, eventos e banners): multipart com o campo "file".
        admin.MapPost("/media", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me =>
            {
                IFormFile? file = null;
                if (http.Request.HasFormContentType)
                {
                    try
                    {
                        file = (await http.Request.ReadFormAsync(ct)).Files.GetFile("file");
                    }
                    catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
                    {
                        return Results.Json(new AuthEndpoints.ErrorBody("too-large", "A imagem pode ter no máximo 3 MB."), statusCode: StatusCodes.Status413PayloadTooLarge);
                    }
                }

                var upload = file is null ? null : new UploadedFile(file.FileName, file.Length, async token =>
                {
                    using var ms = new MemoryStream((int)Math.Min(file.Length, ImageSanitizer.MaxBytes));
                    await using var s = file.OpenReadStream();
                    await s.CopyToAsync(ms, token);
                    return ms.ToArray();
                });
                var r = await site.UploadMediaAsync(me, upload, AuthEndpoints.Client(http), ct);
                return r.Status == StaffAuthStatus.Ok
                    ? Results.Ok(new { id = r.Id, url = r.Url })
                    : Results.Json(new AuthEndpoints.ErrorBody("validation", r.Errors!["file"], r.Errors), statusCode: StatusCodes.Status400BadRequest);
            }, ct))
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes));
    }
}
