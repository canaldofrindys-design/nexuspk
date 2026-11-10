namespace Priston.Api.Security;

/// <summary>Cabeçalhos de segurança aplicados a todas as respostas da API.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            h["Cross-Origin-Resource-Policy"] = "same-origin";
            h.Remove("X-Powered-By");
            return Task.CompletedTask;
        });
        return next(context);
    }
}
