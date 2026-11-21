using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.Application.Home;
using Priston.GameDatabaseIntegration.Connection;
using static Priston.GameDatabaseIntegration.SqlIdentifier;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>
/// Itens pegos do chão em <c>ItemLogDB.dbo.IL</c> (Flag = <see cref="GameDatabaseOptions.ItemPickupFlag"/>, 0 por padrão),
/// sem contas de GM (<c>UserDB.dbo.GameMasters</c>) e sem a lista de <see cref="IRankingExclusionSource"/>.
/// Só leitura; IP e conta nunca saem daqui.
/// </summary>
internal sealed class ItemLogReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options, IRankingExclusionSource exclusions) : IItemLogReader
{
    public async Task<IReadOnlyList<ItemPickupRow>> ReadRecentPickupsAsync(DateTime sinceLocal, int max, CancellationToken ct)
    {
        if (max <= 0)
        {
            return [];
        }

        var o = options.Value;
        var sql = $"""
            SELECT TOP (@take) il.UserID AS Account, il.CharName AS Name, il.ItemCode AS ItemCode, il.RegistDay AS PickedLocal
            FROM {Quote(o.ItemLogDatabase)}.dbo.IL AS il
            WHERE il.Flag = @flag AND il.RegistDay >= @since AND il.ItemCode IS NOT NULL
              AND il.CharName IS NOT NULL AND LTRIM(RTRIM(il.CharName)) <> ''
              AND {NotGameMaster(o)}
            ORDER BY il.RegistDay DESC
            """;
        var rows = await QueryAsync(sql, new { take = max, flag = o.ItemPickupFlag, since = sinceLocal }, ct).ConfigureAwait(false);
        var hidden = await exclusions.GetAsync(ct).ConfigureAwait(false);
        return rows
            .Where(r => r.ItemCode is not null && r.PickedLocal is not null && !hidden.HidesPerson(r.Account, r.Name))
            .Select(r => new ItemPickupRow(r.ItemCode!.Value, r.Name!.Trim(), r.PickedLocal!.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<ItemPickupStat>> ReadTopPickupsAsync(DateTime sinceLocal, int max, CancellationToken ct)
    {
        if (max <= 0)
        {
            return [];
        }

        var o = options.Value;
        var sql = $"""
            SELECT il.UserID AS Account, il.CharName AS Name, il.ItemCode AS ItemCode, il.RegistDay AS PickedLocal
            FROM {Quote(o.ItemLogDatabase)}.dbo.IL AS il
            WHERE il.Flag = @flag AND il.RegistDay >= @since AND il.ItemCode IS NOT NULL
              AND il.CharName IS NOT NULL AND LTRIM(RTRIM(il.CharName)) <> ''
              AND {NotGameMaster(o)}
            """;
        var rows = await QueryAsync(sql, new { flag = o.ItemPickupFlag, since = sinceLocal }, ct).ConfigureAwait(false);
        var hidden = await exclusions.GetAsync(ct).ConfigureAwait(false);
        return rows
            .Where(r => r.ItemCode is not null && r.PickedLocal is not null && !hidden.HidesPerson(r.Account, r.Name))
            .GroupBy(r => r.ItemCode!.Value)
            .Select(g =>
            {
                var last = g.MaxBy(r => r.PickedLocal)!;
                return new ItemPickupStat(g.Key, g.Count(), last.PickedLocal!.Value, last.Name!.Trim());
            })
            .OrderByDescending(s => s.Times)
            .ThenByDescending(s => s.LastLocal)
            .Take(max)
            .ToList();
    }

    private static string NotGameMaster(GameDatabaseOptions o) => $"""
        NOT EXISTS (
                  SELECT 1 FROM {Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                  WHERE gm.Account COLLATE DATABASE_DEFAULT = il.UserID COLLATE DATABASE_DEFAULT)
        """;

    private async Task<IEnumerable<Row>> QueryAsync(string sql, object args, CancellationToken ct)
    {
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        return await conn.QueryAsync<Row>(new CommandDefinition(sql, args, commandTimeout: options.Value.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed record Row(string? Account, string? Name, int? ItemCode, DateTime? PickedLocal);
}
