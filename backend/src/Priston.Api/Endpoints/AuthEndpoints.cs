using Microsoft.AspNetCore.Mvc;
using Priston.Application.Accounts;

namespace Priston.Api.Endpoints;

/// <summary>
/// Cadastro, login e recuperação. A API só escuta em 127.0.0.1; quem chama é o servidor do Next,
/// que guarda o token de sessão em cookie httpOnly e repassa no cabeçalho Authorization.
/// </summary>
public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public sealed record ErrorBody(string Code, string Message, IReadOnlyDictionary<string, string>? Errors = null);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/register", async ([FromBody] RegisterRequest body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var result = await auth.RegisterAsync(body, Client(http), ct);
            return result.Status == AuthStatus.Ok
                ? Results.Json(new { confirmationEmailSent = result.ConfirmationEmailSent }, statusCode: StatusCodes.Status201Created)
                : Error(result.Status, result.Errors);
        });

        group.MapPost("/login", async ([FromBody] LoginRequest body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(body, Client(http), ct);
            return result.Status == AuthStatus.Ok
                ? Results.Ok(new { token = result.SessionToken, expiresUtc = result.ExpiresUtc })
                : Error(result.Status, null);
        });

        group.MapPost("/logout", async (AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            await auth.LogoutAsync(BearerToken(http), ct);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/auth/me", async (AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var me = await auth.GetCurrentAsync(BearerToken(http), ct);
            return me is null ? Results.Json(new ErrorBody("unauthenticated", "Sessão expirada. Entre novamente."), statusCode: 401) : Results.Ok(me);
        });

        group.MapPost("/recover", async ([FromBody] RecoveryRequest body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var result = await auth.RequestPasswordRecoveryAsync(body, Client(http), ct);
            return result.Status == AuthStatus.Ok ? Results.Accepted() : Error(result.Status, result.Errors);
        });

        group.MapPost("/reset-password", async ([FromBody] ResetPasswordRequest body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var result = await auth.ResetPasswordAsync(body, Client(http), ct);
            return result.Status == AuthStatus.Ok ? Results.NoContent() : Error(result.Status, result.Errors);
        });

        group.MapPost("/confirm-email", async ([FromBody] TokenBody body, AuthService auth, HttpContext http, CancellationToken ct) =>
        {
            var result = await auth.ConfirmEmailAsync(body.Token, Client(http), ct);
            return result.Status == AuthStatus.Ok ? Results.NoContent() : Error(result.Status, null);
        });

        app.MapGet("/api/v1/auth/options", (Microsoft.Extensions.Options.IOptions<AccountOptions> o, IGameAccountStore store, IEmailSender email, ICaptchaVerifier captcha) => Results.Ok(new
        {
            registrationOpen = store.WritesEnabled,
            recoveryAvailable = store.WritesEnabled && email.IsConfigured,
            captchaEnabled = captcha.IsEnabled,
            loginMinLength = o.Value.LoginMinLength,
            loginMaxLength = o.Value.LoginMaxLength,
            passwordMinLength = o.Value.PasswordMinLength,
            passwordMaxLength = o.Value.PasswordMaxLength,
        }));
    }

    public sealed record TokenBody(string? Token);

    internal static string? BearerToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Trim() : null;
    }

    private static ClientInfo Client(HttpContext http) =>
        new(http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString());

    private static IResult Error(AuthStatus status, IReadOnlyList<FieldError>? errors)
    {
        var fieldErrors = errors?.ToDictionary(e => e.Field.ToString().ToLowerInvariant(), e => e.Message);
        return status switch
        {
            AuthStatus.ValidationFailed => Results.Json(new ErrorBody("validation", "Confira os campos destacados.", fieldErrors), statusCode: 400),
            AuthStatus.CaptchaFailed => Results.Json(new ErrorBody("captcha", "Confirme que você não é um robô e tente de novo."), statusCode: 400),
            AuthStatus.LoginTaken => Results.Json(new ErrorBody("login-taken", "Este login já está em uso.", new Dictionary<string, string> { ["login"] = "Este login já está em uso." }), statusCode: 409),
            AuthStatus.EmailTaken => Results.Json(new ErrorBody("email-taken", "Este e-mail já está ligado a outra conta.", new Dictionary<string, string> { ["email"] = "Este e-mail já está ligado a outra conta." }), statusCode: 409),
            AuthStatus.InvalidCredentials => Results.Json(new ErrorBody("invalid-credentials", "Login ou senha incorretos."), statusCode: 401),
            AuthStatus.Blocked => Results.Json(new ErrorBody("blocked", "Esta conta está bloqueada. Fale com o suporte."), statusCode: 403),
            AuthStatus.TooManyAttempts => Results.Json(new ErrorBody("too-many-attempts", "Muitas tentativas. Aguarde alguns minutos e tente de novo."), statusCode: 429),
            AuthStatus.InvalidToken => Results.Json(new ErrorBody("invalid-token", "Link inválido ou expirado. Peça um novo."), statusCode: 400),
            AuthStatus.Unavailable => Results.Json(new ErrorBody("unavailable", "Função temporariamente indisponível. Tente mais tarde ou fale com o suporte."), statusCode: 503),
            _ => Results.Json(new ErrorBody("error", "Não foi possível concluir. Tente de novo."), statusCode: 500),
        };
    }
}
