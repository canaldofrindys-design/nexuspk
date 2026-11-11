using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Priston.Application.Admin;

namespace Priston.Api.Endpoints;

/// <summary>
/// Painel administrativo. Sessão de equipe no cabeçalho Authorization (o Next guarda o token em cookie httpOnly próprio).
/// Cada rota confere a função do membro; quem precisa trocar a senha só acessa /me, /password e /logout.
/// </summary>
public static class AdminEndpoints
{
    private sealed record Gate(StaffPrincipal? Me, IResult? Denied);

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin");
        group.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });

        group.MapPost("/login", async ([FromBody] StaffLoginRequest body, StaffAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var r = await auth.LoginAsync(body, AuthEndpoints.Client(http), ct);
            return r.Status switch
            {
                StaffAuthStatus.Ok => Results.Ok(new { token = r.Token, expiresUtc = r.ExpiresUtc, mustChangePassword = r.MustChangePassword }),
                StaffAuthStatus.Locked => Err("locked", "Conta bloqueada por 15 minutos depois de várias senhas erradas.", 423),
                StaffAuthStatus.TooManyAttempts => Err("too-many-attempts", "Muitas tentativas. Aguarde alguns minutos.", 429),
                _ => Err("invalid-credentials", "Usuário ou senha incorretos.", 401),
            };
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        group.MapPost("/logout", async (StaffAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            await auth.LogoutAsync(AuthEndpoints.BearerToken(http), ct);
            return Results.NoContent();
        });

        group.MapGet("/me", async (StaffAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
            return me is null
                ? Unauthenticated()
                : Results.Ok(new
                {
                    me.Id,
                    me.Username,
                    me.DisplayName,
                    me.Role,
                    RoleLabel = StaffRoles.Labels[me.Role],
                    me.MustChangePassword,
                    me.SessionExpiresUtc,
                    Capabilities = StaffRoles.CapabilitiesOf(me.Role).Select(c => c.ToString()),
                });
        });

        group.MapPost("/password", async ([FromBody] StaffChangePasswordRequest body, StaffAuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var token = AuthEndpoints.BearerToken(http);
            var me = await auth.GetCurrentAsync(token, ct);
            if (me is null)
            {
                return Unauthenticated();
            }

            return Result(await auth.ChangePasswordAsync(me, token!, body, AuthEndpoints.Client(http), ct));
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        group.MapGet("/dashboard", async (StaffAuthService auth, DashboardService dashboard, HealthCheckService health, HttpContext http, CancellationToken ct) =>
        {
            var g = await Require(auth, http, Capability.ViewDashboard, ct);
            if (g.Denied is not null)
            {
                return g.Denied;
            }

            var data = await dashboard.GetAsync(ct);
            var report = await health.CheckHealthAsync(c => c.Tags.Contains("ready"), ct);
            return Results.Ok(new
            {
                data.Game,
                data.Portal,
                data.Server,
                data.GeneratedUtc,
                Integration = report.Entries.Select(e => new { Name = e.Key, Status = e.Value.Status.ToString(), Ms = (int)e.Value.Duration.TotalMilliseconds }),
            });
        });

        // Notícias
        group.MapGet("/news", async (StaffAuthService auth, ContentAdminService content, HttpContext http, string? situacao, int? pagina, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await content.ListNewsAsync(situacao, pagina ?? 1, ct)), ct));

        group.MapGet("/news/{id:int}", async (StaffAuthService auth, ContentAdminService content, HttpContext http, int id, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async _ => await content.GetNewsAsync(id, ct) is { } n ? Results.Ok(n) : Results.NotFound(), ct));

        group.MapPost("/news", async ([FromBody] NewsInput body, StaffAuthService auth, ContentAdminService content, HttpContext http, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async me => Saved(await content.SaveNewsAsync(me, null, body, AuthEndpoints.Client(http), ct)), ct));

        group.MapPut("/news/{id:int}", async ([FromBody] NewsInput body, StaffAuthService auth, ContentAdminService content, HttpContext http, int id, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async me => Saved(await content.SaveNewsAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct));

        // Eventos
        group.MapGet("/events", async (StaffAuthService auth, ContentAdminService content, HttpContext http, string? situacao, int? pagina, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async _ => Results.Ok(await content.ListEventsAsync(situacao, pagina ?? 1, ct)), ct));

        group.MapGet("/events/{id:int}", async (StaffAuthService auth, ContentAdminService content, HttpContext http, int id, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async _ => await content.GetEventAsync(id, ct) is { } e ? Results.Ok(e) : Results.NotFound(), ct));

        group.MapPost("/events", async ([FromBody] EventInput body, StaffAuthService auth, ContentAdminService content, HttpContext http, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async me => Saved(await content.SaveEventAsync(me, null, body, AuthEndpoints.Client(http), ct)), ct));

        group.MapPut("/events/{id:int}", async ([FromBody] EventInput body, StaffAuthService auth, ContentAdminService content, HttpContext http, int id, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageContent, async me => Saved(await content.SaveEventAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct));

        // Equipe
        group.MapGet("/staff", async (StaffAuthService auth, StaffAdminService staff, HttpContext http, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageStaff, async _ => Results.Ok(await staff.ListAsync(ct)), ct));

        group.MapPost("/staff", async ([FromBody] CreateStaffRequest body, StaffAuthService auth, StaffAdminService staff, HttpContext http, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageStaff, async me => Result(await staff.CreateAsync(me, body, AuthEndpoints.Client(http), ct), StatusCodes.Status201Created), ct));

        group.MapPut("/staff/{id:int}", async ([FromBody] UpdateStaffRequest body, StaffAuthService auth, StaffAdminService staff, HttpContext http, int id, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageStaff, async me => Result(await staff.UpdateAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct));

        group.MapPost("/staff/{id:int}/password", async ([FromBody] ResetStaffPasswordRequest body, StaffAuthService auth, StaffAdminService staff, HttpContext http, int id, CancellationToken ct) =>
            await Guard(auth, http, Capability.ManageStaff, async me => Result(await staff.ResetPasswordAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct));

        // Auditoria
        group.MapGet("/audit", async (StaffAuthService auth, AuditQueryService audit, HttpContext http, string? tipo, string? autor, string? acao, int? pagina, CancellationToken ct) =>
            await Guard(auth, http, Capability.ViewAudit, async _ => Results.Ok(await audit.ListAsync(new AuditFilter(tipo, autor, acao), pagina ?? 1, ct)), ct));
    }

    internal static async Task<IResult> Guard(StaffAuthService auth, HttpContext http, Capability capability, Func<StaffPrincipal, Task<IResult>> action, CancellationToken ct)
    {
        var g = await Require(auth, http, capability, ct);
        return g.Denied ?? await action(g.Me!);
    }

    private static async Task<Gate> Require(StaffAuthService auth, HttpContext http, Capability capability, CancellationToken ct)
    {
        var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
        if (me is null)
        {
            return new(null, Unauthenticated());
        }

        if (me.MustChangePassword)
        {
            return new(me, Err("must-change-password", "Troque a senha temporária antes de continuar.", 403));
        }

        return me.Can(capability) ? new(me, null) : new(me, Err("forbidden", "Sua função não permite esta ação.", 403));
    }

    internal static IResult Saved(SaveResult r) => r.Status switch
    {
        StaffAuthStatus.Ok => Results.Ok(new { id = r.Id }),
        StaffAuthStatus.NotFound => Results.NotFound(),
        _ => Results.Json(new AuthEndpoints.ErrorBody("validation", "Confira os campos destacados.", r.Errors), statusCode: 400),
    };

    private static IResult Result(StaffResult r, int okStatus = StatusCodes.Status204NoContent) => r.Status switch
    {
        StaffAuthStatus.Ok => Results.StatusCode(okStatus),
        StaffAuthStatus.NotFound => Results.NotFound(),
        _ => Results.Json(new AuthEndpoints.ErrorBody("validation", "Confira os campos destacados.", r.Errors), statusCode: 400),
    };

    private static IResult Unauthenticated() => Err("unauthenticated", "Sessão expirada. Entre novamente.", 401);

    private static IResult Err(string code, string message, int status) => Results.Json(new AuthEndpoints.ErrorBody(code, message), statusCode: status);
}
