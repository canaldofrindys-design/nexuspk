using Priston.Application.Home;

namespace Priston.Api.Endpoints;

public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/home", async (HomeService home, HttpContext http, CancellationToken ct) =>
        {
            var snapshot = await home.GetAsync(ct);
            http.Response.Headers.CacheControl = "public, max-age=30";
            return Results.Ok(snapshot);
        });
    }
}
