using Microsoft.AspNetCore.Mvc;
using Priston.Application.Accounts;
using Priston.Application.Player;

namespace Priston.Api.Endpoints;

/// <summary>Painel do jogador. Todas as rotas exigem sessão (token no cabeçalho Authorization).</summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/player");

        group.MapGet("/panel", async (AuthService auth, PlayerPanelService panel, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
            return me is null ? Unauthenticated() : Results.Ok(await panel.GetAsync(me.Login, ct));
        });

        group.MapPost("/password", async ([FromBody] ChangePasswordRequest body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var token = AuthEndpoints.BearerToken(http);
            var me = await auth.GetCurrentAsync(token, ct);
            if (me is null)
            {
                return Unauthenticated();
            }

            var result = await auth.ChangePasswordAsync(me.Login, token!, body, AuthEndpoints.Client(http), ct);
            return result.Status switch
            {
                AuthStatus.Ok => Results.NoContent(),
                // Senha atual errada: 400 com erro no campo, não 401 (a sessão continua válida).
                AuthStatus.InvalidCredentials => Results.Json(
                    new AuthEndpoints.ErrorBody("invalid-password", "Senha atual incorreta.", new Dictionary<string, string> { ["currentPassword"] = "Senha atual incorreta." }),
                    statusCode: 400),
                _ => AuthEndpoints.Error(result.Status, result.Errors),
            };
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        group.MapPost("/email", async ([FromBody] ChangeEmailRequest body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
            if (me is null)
            {
                return Unauthenticated();
            }

            var result = await auth.ChangeEmailAsync(me.Login, body, AuthEndpoints.Client(http), ct);
            return result.Status == AuthStatus.Ok
                ? Results.Ok(new { confirmationEmailSent = result.ConfirmationEmailSent })
                : AuthEndpoints.Error(result.Status, result.Errors);
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        group.MapPost("/email/resend", async (AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var me = await auth.GetCurrentAsync(AuthEndpoints.BearerToken(http), ct);
            if (me is null)
            {
                return Unauthenticated();
            }

            var result = await auth.ResendEmailConfirmationAsync(me.Login, ct);
            return result.Status == AuthStatus.Ok ? Results.NoContent() : AuthEndpoints.Error(result.Status, null);
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        group.MapPost("/sessions/revoke-others", async (AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var token = AuthEndpoints.BearerToken(http);
            var me = await auth.GetCurrentAsync(token, ct);
            if (me is null)
            {
                return Unauthenticated();
            }

            var count = await auth.RevokeOtherSessionsAsync(me.Login, token!, AuthEndpoints.Client(http), ct);
            return Results.Ok(new { revoked = count });
        }).RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
    }

    private static IResult Unauthenticated() =>
        Results.Json(new AuthEndpoints.ErrorBody("unauthenticated", "Sessão expirada. Entre novamente."), statusCode: StatusCodes.Status401Unauthorized);
}
