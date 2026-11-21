using System.Globalization;
using Dapper;
using Microsoft.Extensions.Options;
using Priston.Application.Home;
using Priston.GameDatabaseIntegration.Connection;
using static Priston.GameDatabaseIntegration.SqlIdentifier;

namespace Priston.GameDatabaseIntegration.Characters;

/// <summary>
/// Nomes dos tipos de item de <c>ItemDB.dbo.ItemType</c> (ItemTypeName, ItemTypeHex, ItemTypeAbbr), pela chave grupo+tipo do ItemCode.
/// ItemTypeHex pode vir como texto ("0x01010000"), número ou varbinary(4) (o caso deste servidor); sem ele, vale ItemTypeId
/// (0x01010000 = 16842752). A coluna da sigla se chama ItemTypeAbbrv neste servidor (ItemTypeAbbr em outros). Só leitura.
/// </summary>
internal sealed class ItemTypeNameReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IItemNameReader
{
    public async Task<IReadOnlyList<ItemTypeInfo>> ReadTypesAsync(CancellationToken ct)
    {
        var o = options.Value;
        var table = $"{Quote(o.ItemDatabase)}.dbo.ItemType";
        var name = $"{o.ItemDatabase}.dbo.ItemType".Replace("'", "''", StringComparison.Ordinal);
        var sql = $"""
            IF COL_LENGTH('{name}', 'ItemTypeAbbrv') IS NOT NULL
                SELECT ItemTypeId AS Id, ItemTypeName AS Name, ItemTypeHex AS Hex, ItemTypeAbbrv AS Abbr FROM {table};
            ELSE
                SELECT ItemTypeId AS Id, ItemTypeName AS Name, ItemTypeHex AS Hex, ItemTypeAbbr AS Abbr FROM {table};
            """;
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        var list = new List<ItemTypeInfo>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Name) || (Key(r.Hex) ?? Key(r.Id)) is not { } key)
            {
                continue;
            }

            list.Add(new ItemTypeInfo(key, r.Name.Trim(), string.IsNullOrWhiteSpace(r.Abbr) ? null : r.Abbr.Trim()));
        }

        return list;
    }

    internal static int? Key(object? hex) => hex switch
    {
        null => null,
        string s => GameItemCode.ParseTypeHex(s),
        byte[] { Length: > 0 and <= 8 } b => FromNumber(b.Aggregate(0L, (acc, x) => (acc << 8) | x)),
        int or long or short or byte or decimal => FromNumber(Convert.ToInt64(hex, CultureInfo.InvariantCulture)),
        _ => GameItemCode.ParseTypeHex(Convert.ToString(hex, CultureInfo.InvariantCulture)),
    };

    private static int? FromNumber(long v) => v switch
    {
        <= 0 => null,
        <= 0xFFFF => (int)v,
        _ => (int)((v >> 16) & 0xFFFF),
    };

    // Classe com propriedades (não record posicional): o Dapper preenche Hex com o tipo que vier do banco.
    private sealed class Row
    {
        public object? Id { get; set; }

        public string? Name { get; set; }

        public object? Hex { get; set; }

        public string? Abbr { get; set; }
    }
}
