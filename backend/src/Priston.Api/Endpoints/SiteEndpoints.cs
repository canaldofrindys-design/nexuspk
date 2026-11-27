using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Priston.Application.Admin;
using Priston.Application.Home;
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

        // Imagem de um item do jogo (copiada do cliente). Público: é a mesma imagem que todo jogador tem no cliente.
        // Página pública do item: atributos (OpenItem), de quais monstros cai (Monster) e quais NPCs vendem (NPC).
        app.MapGet("/api/v1/items/{id}", async (ItemGuide guide, HttpContext http, string id, CancellationToken ct) =>
        {
            if (await guide.GetAsync(id, ct) is not { } page)
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(page);
        });

        // Banco de monstros público: lista (com busca por monstro ou item) e página de cada monstro, dos arquivos Monster\*.inf.
        app.MapGet("/api/v1/monsters", async (MonsterGuide guide, HttpContext http, string? q, CancellationToken ct) =>
        {
            if (q is { Length: > MonsterGuide.MaxQueryLength })
            {
                return Results.BadRequest();
            }

            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(await guide.ListAsync(q, ct));
        });

        app.MapGet("/api/v1/monsters/{slug}", async (MonsterGuide guide, HttpContext http, string slug, CancellationToken ct) =>
        {
            if (slug.Length is 0 or > 120 || await guide.GetAsync(slug, ct) is not { } page)
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(page);
        });

        app.MapGet("/api/v1/items/{id}/image", async (IServiceProvider services, HttpContext http, string id, CancellationToken ct) =>
        {
            if (ItemNames.ParseFileId(id) is null || services.GetService<IItemImageStore>() is not { } images)
            {
                return Results.NotFound();
            }

            ItemImage? image;
            try
            {
                image = await images.GetAsync(id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                image = null;
            }

            if (image is null)
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=86400";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(image.Data, image.ContentType);
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

        // Drops raros da semana (mesma permissão de notícias, eventos e banners)
        admin.MapGet("/site/drops", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await site.GetRareDropSettingsAsync(ct)), ct));

        admin.MapPut("/site/drops", async ([FromBody] RareDropSettingsInput body, StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await site.SaveRareDropSettingsAsync(me, body, AuthEndpoints.Client(http), ct)), ct));

        // Itens raros: todo "pegou" desses códigos no log de itens vira drop na home.
        admin.MapGet("/site/rare-items", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await site.GetRareItemSettingsAsync(ct)), ct));

        admin.MapPut("/site/rare-items", async ([FromBody] RareItemSettingsInput body, StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await site.SaveRareItemSettingsAsync(me, body, AuthEndpoints.Client(http), ct)), ct));

        // Horários fixos dos eventos do servidor (Bless Castle, SoD...): contagem regressiva no banner da home.
        admin.MapGet("/site/schedule", async (StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await site.GetScheduleSettingsAsync(ct)), ct));

        admin.MapPut("/site/schedule", async ([FromBody] EventScheduleSettingsInput body, StaffAuthService auth, SiteContentService site, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async me => AdminEndpoints.Saved(await site.SaveScheduleSettingsAsync(me, body, AuthEndpoints.Client(http), ct)), ct));

        // Itens mais pegos nos últimos 30 dias (log de itens), para a equipe achar os códigos dos raros.
        admin.MapGet("/site/item-pickups", async (StaffAuthService auth, IServiceProvider services, TimeProvider clock, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageContent, async _ =>
            {
                if (services.GetService<IItemLogReader>() is not { } itemLog)
                {
                    return Results.Ok(new { available = false, items = Array.Empty<ItemPickupStat>() });
                }

                try
                {
                    var zone = TimeZoneInfo.FindSystemTimeZoneById(Priston.Domain.Time.RankingPeriods.TimeZoneId);
                    var since = TimeZoneInfo.ConvertTime(clock.GetUtcNow().AddDays(-30), zone).DateTime;
                    var items = await itemLog.ReadTopPickupsAsync(since, 500, ct);
                    var names = services.GetService<ItemNameSource>() is { } source ? await source.GetAsync(ct) : ItemNames.Empty;
                    return Results.Ok(new
                    {
                        available = true,
                        exactNames = names.ExactCount,
                        dropSources = names.SourceCount,
                        items = items.Select(i => new { i.ItemCode, name = names.Name(i.ItemCode), boss = names.Boss(i.ItemCode), i.Times, lastUtc = RareDropFeed.ToUtc(i.LastLocal), i.LastCharacter }),
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Results.Ok(new { available = false, items = Array.Empty<ItemPickupStat>() });
                }
            }, ct));

        // Catálogo de itens do servidor (nomes dos arquivos OpenItem e quais têm imagem), para escolher item pela foto.
        admin.MapGet("/items", async (StaffAuthService auth, IServiceProvider services, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async _ =>
            {
                var names = services.GetService<ItemNameSource>() is { } source ? await source.GetAsync(ct) : ItemNames.Empty;
                IReadOnlySet<string> withImage = new HashSet<string>();
                if (services.GetService<IItemImageStore>() is { } images)
                {
                    try
                    {
                        withImage = await images.ListIdsAsync(ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        withImage = new HashSet<string>();
                    }
                }

                var ids = names.FileNames.Keys.Concat(withImage).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
                return Results.Ok(new
                {
                    images = withImage.Count,
                    items = ids.Select(id => new { id, name = names.FileNames.GetValueOrDefault(id), image = withImage.Contains(id) }),
                });
            }, ct));

        // Drops dos monstros lidos dos arquivos do servidor (Monster\*.inf), com nome e imagem dos itens. Só consulta.
        admin.MapGet("/monsters", async (StaffAuthService auth, IServiceProvider services, IMemoryCache cache, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async _ =>
            {
                if (services.GetService<IMonsterDropCatalog>() is not { } monsters)
                {
                    return Results.Ok(new { available = false, monsters = Array.Empty<object>() });
                }

                try
                {
                    var list = await cache.GetOrCreateAsync("admin:monsters", async e =>
                    {
                        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return await monsters.ReadMonstersAsync(ct);
                    }) ?? [];
                    var names = services.GetService<ItemNameSource>() is { } source ? await source.GetAsync(ct) : ItemNames.Empty;
                    IReadOnlySet<string> withImage = new HashSet<string>();
                    if (services.GetService<IItemImageStore>() is { } images)
                    {
                        try
                        {
                            withImage = await images.ListIdsAsync(ct);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            withImage = new HashSet<string>();
                        }
                    }

                    return Results.Ok(new
                    {
                        available = true,
                        monsters = list.Select(m => new
                        {
                            m.Name,
                            m.File,
                            m.Hp,
                            drops = m.Drops.Select(d => new
                            {
                                d.Chance,
                                d.Gold,
                                items = d.Items.Select(id => new { id, name = names.FileNames.GetValueOrDefault(id), image = withImage.Contains(id) }),
                            }),
                        }),
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Results.Ok(new { available = false, monsters = Array.Empty<object>() });
                }
            }, ct));

        // Lojas dos NPCs lidas dos arquivos do servidor (NPC\*.npc), com nome e imagem dos itens. Só consulta.
        admin.MapGet("/npc-shops", async (StaffAuthService auth, IServiceProvider services, IMemoryCache cache, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async _ =>
            {
                if (services.GetService<INpcShopCatalog>() is not { } npcs)
                {
                    return Results.Ok(new { available = false, shops = Array.Empty<object>() });
                }

                try
                {
                    var list = await cache.GetOrCreateAsync("admin:npc-shops", async e =>
                    {
                        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return await npcs.ReadShopsAsync(ct);
                    }) ?? [];
                    var names = services.GetService<ItemNameSource>() is { } source ? await source.GetAsync(ct) : ItemNames.Empty;
                    IReadOnlySet<string> withImage = new HashSet<string>();
                    if (services.GetService<IItemImageStore>() is { } images)
                    {
                        try
                        {
                            withImage = await images.ListIdsAsync(ct);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            withImage = new HashSet<string>();
                        }
                    }

                    object Items(IReadOnlyList<string> ids) => ids.Select(id => new { id, name = names.FileNames.GetValueOrDefault(id), image = withImage.Contains(id) });
                    return Results.Ok(new
                    {
                        available = true,
                        shops = list.Select(s => new { s.Name, s.File, weapons = Items(s.Weapons), armors = Items(s.Armors), misc = Items(s.Misc) }),
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Results.Ok(new { available = false, shops = Array.Empty<object>() });
                }
            }, ct));

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
