using System.Text;
using System.Text.RegularExpressions;
using Priston.Application.Accounts;
using Priston.Application.Content;

namespace Priston.Api.Endpoints;

/// <summary>Notícias e eventos (públicos) e o lembrete de evento (exige sessão).</summary>
public static partial class ContentEndpoints
{
    public static void MapContentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1");

        group.MapGet("/news", async (ContentService content, HttpContext http, string? categoria, int? pagina, int? porPagina, CancellationToken ct) =>
        {
            if (categoria is not null && !NewsCategories.IsValid(categoria))
            {
                return Results.BadRequest();
            }

            PublicCache(http);
            return Results.Ok(await content.ListNewsAsync(categoria, pagina ?? 1, porPagina ?? 10, ct));
        });

        group.MapGet("/news/{slug}", async (ContentService content, HttpContext http, string slug, CancellationToken ct) =>
        {
            if (!ValidSlug(slug))
            {
                return Results.NotFound();
            }

            var article = await content.GetNewsAsync(slug, ct);
            PublicCache(http);
            return article is null ? Results.NotFound() : Results.Ok(article);
        });

        group.MapGet("/events", async (ContentService content, HttpContext http, CancellationToken ct) =>
        {
            PublicCache(http);
            return Results.Ok(await content.GetOverviewAsync(ct));
        });

        group.MapGet("/events/calendar", async (ContentService content, HttpContext http, int? ano, int? mes, CancellationToken ct) =>
        {
            if (ano is null or < 2020 or > 2100 || mes is null or < 1 or > 12)
            {
                return Results.BadRequest();
            }

            PublicCache(http);
            return Results.Ok(await content.GetCalendarAsync(ano.Value, mes.Value, ct));
        });

        group.MapGet("/events/{slug}", async (ContentService content, AuthService auth, HttpContext http, string slug, CancellationToken ct) =>
        {
            if (!ValidSlug(slug))
            {
                return Results.NotFound();
            }

            // Com sessão a resposta inclui o lembrete do jogador e não pode ir para cache compartilhado.
            var token = AuthEndpoints.BearerToken(http);
            string? login = null;
            if (token is not null)
            {
                login = (await auth.GetCurrentAsync(token, ct))?.Login;
                http.Response.Headers.CacheControl = "private, no-store";
            }
            else
            {
                PublicCache(http);
            }

            var detail = await content.GetEventAsync(slug, login, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        group.MapGet("/events/{slug}/ics", async (ContentService content, HttpContext http, string slug, CancellationToken ct) =>
        {
            if (!ValidSlug(slug))
            {
                return Results.NotFound();
            }

            var ics = await content.GetIcsAsync(slug, ct);
            if (ics is null)
            {
                return Results.NotFound();
            }

            PublicCache(http);
            return Results.File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", $"{slug}.ics");
        });

        group.MapPut("/player/events/{slug}/reminder", (ContentService content, AuthService auth, HttpContext http, string slug, CancellationToken ct) =>
            SetReminder(content, auth, http, slug, true, ct)).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        group.MapDelete("/player/events/{slug}/reminder", (ContentService content, AuthService auth, HttpContext http, string slug, CancellationToken ct) =>
            SetReminder(content, auth, http, slug, false, ct)).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
    }

    private static async Task<IResult> SetReminder(ContentService content, AuthService auth, HttpContext http, string slug, bool enabled, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
        if (me is null)
        {
            return Results.Json(new AuthEndpoints.ErrorBody("unauthenticated", "Sessão expirada. Entre novamente."), statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!ValidSlug(slug))
        {
            return Results.NotFound();
        }

        return await content.SetReminderAsync(me.Login, slug, enabled, ct) switch
        {
            ReminderResult.Ok => Results.NoContent(),
            ReminderResult.NotFound => Results.NotFound(),
            ReminderResult.NotScheduled => Results.Json(
                new AuthEndpoints.ErrorBody("event-not-scheduled", "Este evento já começou ou foi cancelado."), statusCode: StatusCodes.Status409Conflict),
            ReminderResult.EmailNotConfirmed => Results.Json(
                new AuthEndpoints.ErrorBody("email-not-confirmed", "Confirme um e-mail no painel para receber lembretes."), statusCode: StatusCodes.Status409Conflict),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static void PublicCache(HttpContext http) => http.Response.Headers.CacheControl = "public, max-age=60";

    private static bool ValidSlug(string slug) => slug.Length <= 120 && SlugPattern().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
