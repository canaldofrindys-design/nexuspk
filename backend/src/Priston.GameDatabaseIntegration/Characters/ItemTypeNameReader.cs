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
/// (0x01010000 = 16842752). A coluna da sigla se chama ItemTypeAbbrv neste servidor (ItemTypeAbbr em outros), por isso a
/// leitura pega todas as colunas e escolhe a que existir. Só leitura.
/// </summary>
internal sealed class ItemTypeNameReader(IGameDbConnectionFactory connections, IOptions<GameDatabaseOptions> options) : IItemNameReader
{
    public async Task<IReadOnlyList<ItemTypeInfo>> ReadTypesAsync(CancellationToken ct)
    {
        var o = options.Value;

        // SELECT * de propósito: o SQL Server confere as colunas do lote inteiro antes de rodar, então um IF/ELSE com
        // ItemTypeAbbr e ItemTypeAbbrv falha nos dois casos (a coluna que não existe derruba a consulta toda).
        var sql = $"SELECT * FROM {Quote(o.ItemDatabase)}.dbo.ItemType";
        await using var conn = await connections.OpenReadAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync(new CommandDefinition(sql, commandTimeout: o.CommandTimeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);
        return Parse(rows.Cast<IDictionary<string, object?>>());
    }

    /// <summary>Monta os tipos a partir das linhas da tabela, com as colunas que existirem (nomes sem diferença de maiúsculas).</summary>
    internal static IReadOnlyList<ItemTypeInfo> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var list = new List<ItemTypeInfo>();
        foreach (var raw in rows)
        {
            var row = new Dictionary<string, object?>(raw, StringComparer.OrdinalIgnoreCase);
            object? Get(params string[] names) => names.Select(n => row.GetValueOrDefault(n)).FirstOrDefault(v => v is not null and not DBNull);
            var name = Convert.ToString(Get("ItemTypeName"), CultureInfo.InvariantCulture);
            var abbr = Convert.ToString(Get("ItemTypeAbbrv", "ItemTypeAbbr"), CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(name) || (Key(Get("ItemTypeHex")) ?? Key(Get("ItemTypeId"))) is not { } key)
            {
                continue;
            }

            list.Add(new ItemTypeInfo(key, name.Trim(), string.IsNullOrWhiteSpace(abbr) ? null : abbr.Trim()));
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
}
