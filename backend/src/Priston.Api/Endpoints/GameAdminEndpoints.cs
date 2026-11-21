using Microsoft.AspNetCore.Mvc;
using Priston.Application.Admin;
using Priston.Application.GameAdmin;

namespace Priston.Api.Endpoints;

/// <summary>
/// Painel do jogo: contas, banimentos, coins, VIP, servidor, rankings e editores de tabelas do jogo.
/// Cada rota confere a sessão e a função; ações perigosas também pedem a senha (conferida no serviço) e têm o limite
/// de 10 por minuto por IP. Toda escrita vai para a auditoria.
/// </summary>
public static class GameAdminEndpoints
{
    public const string RateLimitPolicy = "game-admin";

    public static void MapGameAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin");
        g.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });
        var limit = RateLimitPolicy;

        // Contas
        g.MapGet("/accounts", async (StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string? q, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewAccounts, async _ => Map(await svc.SearchAsync(q, ct)), ct));

        g.MapGet("/accounts/{login}", async (StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewAccounts, async _ => Map(await svc.GetAsync(login, ct)), ct));

        g.MapPost("/accounts/{login}/ban", async ([FromBody] BanRequest body, StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageAccounts, async me => Map(await svc.BanAsync(me, login, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        g.MapPost("/accounts/{login}/unban", async ([FromBody] UnbanRequest body, StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageAccounts, async me => Map(await svc.UnbanAsync(me, login, body, AuthEndpoints.Client(http), ct)), ct));

        g.MapPost("/accounts/{login}/coins", async ([FromBody] CoinRequest body, StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageEconomy, async me => Map(await svc.ChangeCoinsAsync(me, login, body, AuthEndpoints.Client(http), ct)), ct));

        g.MapPost("/accounts/{login}/vip", async ([FromBody] VipAddRequest body, StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageEconomy, async me => Map(await svc.AddVipAsync(me, login, body, AuthEndpoints.Client(http), ct)), ct));

        g.MapPost("/accounts/{login}/vip/extend", async ([FromBody] VipExtendRequest body, StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageEconomy, async me => Map(await svc.ExtendVipAsync(me, login, body, AuthEndpoints.Client(http), ct)), ct));

        g.MapPost("/accounts/{login}/vip/remove", async ([FromBody] VipRemoveRequest body, StaffAuthService auth, GameAccountAdminService svc, HttpContext http, string login, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageEconomy, async me => Map(await svc.RemoveVipAsync(me, login, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        // Box Donate: cria o pacote no sistema de donate do servidor, que monta a caixa e entrega ao jogador
        g.MapGet("/donate-box", async (StaffAuthService auth, DonateBoxService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageEconomy, async _ => Map(await svc.OverviewAsync(ct)), ct));

        g.MapPost("/donate-box", async ([FromBody] DonateBoxRequest body, StaffAuthService auth, DonateBoxService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ManageEconomy, async me => Map(await svc.SendAsync(me, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        // Servidor
        g.MapGet("/server", async (StaffAuthService auth, ServerControlService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ControlServer, _ => Task.FromResult(Results.Ok(new
            {
                svc.Enabled,
                Processes = svc.Enabled ? svc.Status() : [],
                ConfirmWords = new { start = ServerControlService.ConfirmWordFor("start"), stop = ServerControlService.ConfirmWordFor("stop"), restart = ServerControlService.ConfirmWordFor("restart") },
            })), ct));

        g.MapPost("/server", async ([FromBody] ServerControlRequest body, StaffAuthService auth, ServerControlService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ControlServer, async me => Map(await svc.RunAsync(me, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        // Rankings
        g.MapGet("/rankings", async (StaffAuthService auth, RankingResetService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ResetRankings, async _ => Results.Ok(new
            {
                svc.Enabled,
                ConfirmWord = RankingResetService.ConfirmWord,
                Kinds = RankingResetKinds.All.Select(k => new { k.Key, k.Label, k.Description }),
                Backups = await svc.ListBackupsAsync(ct),
            }), ct));

        g.MapPost("/rankings/reset", async ([FromBody] RankingResetRequest body, StaffAuthService auth, RankingResetService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ResetRankings, async me => Map(await svc.ResetAsync(me, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        g.MapPost("/rankings/backups/{id:long}/restore", async ([FromBody] RankingRestoreRequest body, StaffAuthService auth, RankingResetService svc, HttpContext http, long id, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ResetRankings, async me => Map(await svc.RestoreAsync(me, id, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        // Editores de tabelas do jogo (a função de cada tabela é conferida no serviço)
        g.MapGet("/game-tables", async (StaffAuthService auth, GameTableService svc, HttpContext http, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async me => Results.Ok(new { svc.Enabled, Functions = await svc.ListAsync(me, ct) }), ct));

        g.MapGet("/game-tables/{key}", async (StaffAuthService auth, GameTableService svc, HttpContext http, string key, int? pagina, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async me =>
            {
                var filters = http.Request.Query
                    .Where(q => q.Key.StartsWith("f.", StringComparison.Ordinal) && q.Key.Length > 2)
                    .ToDictionary(q => q.Key[2..], q => q.Value.ToString()[..Math.Min(q.Value.ToString().Length, 200)], StringComparer.Ordinal);
                return Map(await svc.BrowseAsync(me, key, filters, pagina ?? 1, ct));
            }, ct));

        g.MapPost("/game-tables/{key}/rows", async ([FromBody] RowInsertRequest body, StaffAuthService auth, GameTableService svc, HttpContext http, string key, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async me => Map(await svc.InsertRowAsync(me, key, body, AuthEndpoints.Client(http), ct)), ct));

        g.MapPut("/game-tables/{key}/rows", async ([FromBody] RowUpdateRequest body, StaffAuthService auth, GameTableService svc, HttpContext http, string key, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async me => Map(await svc.UpdateRowAsync(me, key, body, AuthEndpoints.Client(http), ct)), ct));

        g.MapPost("/game-tables/{key}/rows/delete", async ([FromBody] RowDeleteRequest body, StaffAuthService auth, GameTableService svc, HttpContext http, string key, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ViewDashboard, async me => Map(await svc.DeleteRowAsync(me, key, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        // Configuração das fontes (só superadministrador)
        g.MapGet("/game-tables/{key}/config", async (StaffAuthService auth, GameTableService svc, HttpContext http, string key, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ConfigureGameSources, async _ =>
            {
                var f = GameFunctions.Find(key);
                if (f is null)
                {
                    return Results.NotFound();
                }

                var dbs = await svc.DatabasesAsync(ct);
                return Results.Ok(new
                {
                    Function = new { f.Key, f.Label, f.Description, Mode = f.Mode.ToString().ToLowerInvariant(), f.SuggestedDatabases },
                    Binding = await svc.GetBindingAsync(key, ct),
                    Databases = dbs.Status == GameAdminStatus.Ok ? dbs.Data : null,
                    Message = dbs.Status == GameAdminStatus.Ok ? null : dbs.Message,
                });
            }, ct));

        g.MapGet("/game-tables/{key}/config/tables", async (StaffAuthService auth, GameTableService svc, HttpContext http, string key, string? banco, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ConfigureGameSources, async _ => Map(await svc.TablesAsync(key, banco, ct)), ct));

        g.MapGet("/game-tables/{key}/config/columns", async (StaffAuthService auth, GameTableService svc, HttpContext http, string key, string? banco, string? tabela, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ConfigureGameSources, async _ => Map(await svc.ColumnsAsync(banco, tabela, ct)), ct));

        g.MapPut("/game-tables/{key}/config", async ([FromBody] SourceConfigRequest body, StaffAuthService auth, GameTableService svc, HttpContext http, string key, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ConfigureGameSources, async me => Map(await svc.SaveConfigAsync(me, key, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);

        g.MapPost("/game-tables/{key}/config/remove", async ([FromBody] PasswordRequest body, StaffAuthService auth, GameTableService svc, HttpContext http, string key, CancellationToken ct) =>
            await AdminEndpoints.Guard(auth, http, Capability.ConfigureGameSources, async me => Map(await svc.RemoveConfigAsync(me, key, body, AuthEndpoints.Client(http), ct)), ct))
            .RequireRateLimiting(limit);
    }

    internal static IResult Map(GameAdminResult r) => r.Status switch
    {
        GameAdminStatus.Ok => Results.Ok(new { message = r.Message, data = r.Data }),
        GameAdminStatus.ValidationFailed => Error("validation", r.Message ?? "Confira os campos destacados.", 400, r.Errors),
        GameAdminStatus.ReauthFailed => Error("wrong-password", r.Message ?? "Senha incorreta.", 400, r.Errors),
        GameAdminStatus.NotFound => Error("not-found", r.Message ?? "Não encontrado.", 404),
        GameAdminStatus.Forbidden => Error("forbidden", r.Message ?? "Sua função não permite esta ação.", 403),
        GameAdminStatus.Disabled => Error("disabled", r.Message ?? "Função desligada.", 409),
        _ => Error("conflict", r.Message ?? "Não foi possível concluir.", 409),
    };

    private static IResult Error(string code, string message, int status, IReadOnlyDictionary<string, string>? errors = null) =>
        Results.Json(new AuthEndpoints.ErrorBody(code, message, errors), statusCode: status);
}
