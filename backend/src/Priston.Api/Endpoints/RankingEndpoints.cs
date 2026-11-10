using Priston.Application.Characters;

namespace Priston.Api.Endpoints;

/// <summary>Rankings e personagens (públicos, só leitura, servidos de cache).</summary>
public static class RankingEndpoints
{
    public static void MapRankingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1");

        group.MapGet("/rankings/level", async (RankingQueryService rankings, HttpContext http, string? periodo, int? classe, int? pagina, int? porPagina, CancellationToken ct) =>
        {
            var period = periodo switch
            {
                null or "" or "geral" => (LevelPeriod?)LevelPeriod.General,
                "diario" => LevelPeriod.Daily,
                "semanal" => LevelPeriod.Weekly,
                "mensal" => LevelPeriod.Monthly,
                _ => null,
            };
            if (period is null || !ValidClass(classe))
            {
                return Results.BadRequest();
            }

            Cache(http);
            return Results.Ok(await rankings.GetLevelAsync(period.Value, classe, pagina ?? 1, porPagina ?? 50, ct));
        });

        group.MapGet("/rankings/pvp", async (RankingQueryService rankings, HttpContext http, string? tipo, int? classe, int? pagina, int? porPagina, CancellationToken ct) =>
        {
            var kind = tipo switch
            {
                null or "" or "bp" => (PvpKind?)PvpKind.BattlePoints,
                "semanal" => PvpKind.Weekly,
                "abates" => PvpKind.Kills,
                _ => null,
            };
            if (kind is null || !ValidClass(classe))
            {
                return Results.BadRequest();
            }

            Cache(http);
            return Results.Ok(await rankings.GetPvpAsync(kind.Value, classe, pagina ?? 1, porPagina ?? 50, ct));
        });

        group.MapGet("/rankings/clans", async (RankingQueryService rankings, HttpContext http, int? pagina, int? porPagina, CancellationToken ct) =>
        {
            Cache(http);
            return Results.Ok(await rankings.GetClansAsync(pagina ?? 1, porPagina ?? 50, ct));
        });

        group.MapGet("/characters", async (RankingQueryService rankings, HttpContext http, string? q, CancellationToken ct) =>
        {
            if (q is null || q.Length > 32)
            {
                return Results.BadRequest();
            }

            Cache(http);
            return Results.Ok(await rankings.SearchAsync(q, ct));
        });

        group.MapGet("/characters/{name}", async (RankingQueryService rankings, HttpContext http, string name, CancellationToken ct) =>
        {
            if (name.Length is 0 or > 32)
            {
                return Results.NotFound();
            }

            var profile = await rankings.GetCharacterAsync(name, ct);
            Cache(http);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });
    }

    private static bool ValidClass(int? classe) => classe is null or (>= 1 and <= 11);

    private static void Cache(HttpContext http) => http.Response.Headers.CacheControl = "public, max-age=60";
}
