using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Characters;
using Priston.GameDatabaseIntegration.Connection;
using static Priston.GameDatabaseIntegration.SqlIdentifier;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>
/// Aging e craft em <c>ItemLogDB.dbo.IL</c> pelos Flags de <see cref="GameDatabaseOptions.ItemLogAgingFlags"/> e
/// <see cref="GameDatabaseOptions.ItemLogCraftFlags"/>, sem contas de GM (<c>UserDB.dbo.GameMasters</c>) e sem a lista de
/// <see cref="IRankingExclusionSource"/>. Só leitura; IP e conta nunca saem daqui.
/// </summary>
internal sealed class ItemEventLogReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options, IRankingExclusionSource exclusions) : IItemEventLogReader
{
    public bool IsConfigured(ItemEventKind kind) => Flags(kind).Count > 0;

    public async Task<IReadOnlyList<ItemEventRow>> ReadRecentAsync(ItemEventKind kind, DateTime sinceLocal, int max, CancellationToken ct)
    {
        var flags = Flags(kind);
        if (flags.Count == 0 || max <= 0)
        {
            return [];
        }

        var o = options.Value;
        var sql = $"""
            SELECT TOP (@take) il.UserID AS Account, il.CharName AS Name, il.ItemCode AS ItemCode, il.RegistDay AS AtLocal
            FROM {Quote(o.ItemLogDatabase)}.dbo.IL AS il
            WHERE il.Flag IN @flags AND il.RegistDay >= @since AND il.ItemCode IS NOT NULL
              AND il.CharName IS NOT NULL AND LTRIM(RTRIM(il.CharName)) <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM {Quote(o.UserDatabase)}.dbo.GameMasters AS gm
                  WHERE gm.Account COLLATE DATABASE_DEFAULT = il.UserID COLLATE DATABASE_DEFAULT)
            ORDER BY il.RegistDay DESC
            """;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(sql, new { take = max, flags, since = sinceLocal }, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        var hidden = await exclusions.GetAsync(ct).ConfigureAwait(false);
        return rows
            .Where(r => r.ItemCode is not null && r.AtLocal is not null && !hidden.HidesPerson(r.Account, r.Name))
            .Select(r => new ItemEventRow(r.ItemCode!.Value, r.Name!.Trim(), r.AtLocal!.Value))
            .ToList();
    }

    private List<int> Flags(ItemEventKind kind) =>
        (kind == ItemEventKind.Aging ? options.Value.ItemLogAgingFlags : options.Value.ItemLogCraftFlags)?.Distinct().ToList() ?? [];

    private sealed record Row(string? Account, string? Name, int? ItemCode, DateTime? AtLocal);
}
