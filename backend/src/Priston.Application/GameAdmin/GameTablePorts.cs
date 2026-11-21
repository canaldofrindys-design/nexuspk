using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

public enum GameFunctionMode
{
    /// <summary>Listar, procurar, editar; incluir e apagar se a configuração permitir.</summary>
    Edit,

    /// <summary>Só incluir linhas (enviar). A lista mostra os últimos envios.</summary>
    Insert,

    /// <summary>Só consultar.</summary>
    ReadOnly,
}

/// <summary>
/// Função do painel que trabalha numa tabela do jogo ainda não mapeada no DATABASE_MAPPING.md.
/// A tabela não fica escrita no código: o superadministrador escolhe a tabela real na tela "Configurar"
/// e o painel confere, a cada uso, se ela e as colunas ainda existem.
/// </summary>
public sealed record GameFunction(
    string Key,
    string Label,
    string Description,
    GameFunctionMode Mode,
    Capability ReadCapability,
    Capability WriteCapability,
    IReadOnlyList<string> SuggestedDatabases,
    IReadOnlyList<string> Keywords);

public static class GameFunctions
{
    public static readonly IReadOnlyList<GameFunction> All =
    [
        new("box-send", "Enviar Box Donate", "Entrega uma Box Donate (ou item) para a conta do jogador.", GameFunctionMode.Insert,
            Capability.ManageEconomy, Capability.ManageEconomy, ["ServerDB", "NexusPKAdmin", "ItemDB"], ["box", "donat", "entreg", "distrib", "present", "post", "item"]),
        new("box-prices", "Preços do NPC Box Donate", "Preço (em reais) e disponibilidade das Box Donate e dos pacotes de KC vendidos no NPC Box Donate.", GameFunctionMode.Edit,
            Capability.ManageGameData, Capability.ManageGameData, ["ServerDB", "CoinShopDB"], ["box", "donat", "preco", "price", "pacote", "package", "coin"]),
        new("skin-shop", "Loja de Skin (Coin Shop)", "Itens e preços em KC da Coin Shop, com a galeria de skins.", GameFunctionMode.Edit,
            Capability.ManageGameData, Capability.ManageGameData, ["CoinShopDB", "ServerDB"], ["skin", "costume", "traje", "visual", "shop"]),
        new("time-shop", "Loja de minutos", "Itens e preços da loja paga com minutos.", GameFunctionMode.Edit,
            Capability.ManageGameData, Capability.ManageGameData, ["TimeShopDB"], ["time", "tempo", "minut", "shop", "item"]),
        new("monster-drops", "Drops de monstros", "O que cada monstro deixa cair e a chance.", GameFunctionMode.Edit,
            Capability.ManageGameData, Capability.ManageGameData, ["ServerDB", "ItemDB"], ["drop", "monst", "mob", "loot"]),
        new("npc-shops", "Lojas NPC", "Itens vendidos pelos NPCs da cidade.", GameFunctionMode.Edit,
            Capability.ManageGameData, Capability.ManageGameData, ["ServerDB", "ItemDB"], ["npc", "shop", "loja", "venda", "sell"]),
        new("item-catalog", "Catálogo de itens", "Lista de itens do jogo com código e atributos.", GameFunctionMode.Edit,
            Capability.ViewGameLogs, Capability.ManageGameData, ["ItemDB", "ServerDB"], ["item", "catalog", "list"]),
        new("item-logs", "Logs de itens", "Histórico de itens: trocas, compras, drops e entregas.", GameFunctionMode.ReadOnly,
            Capability.ViewGameLogs, Capability.ViewGameLogs, ["ItemLogDB", "GameLogDB"], ["log", "item", "trade", "troca", "hist"]),
    ];

    public static GameFunction? Find(string? key) => All.FirstOrDefault(f => f.Key == key);

    /// <summary>Quem aparece como autor das tabelas que já vêm ligadas.</summary>
    public const string BuiltInAuthor = "padrão do servidor";

    /// <summary>
    /// Tabelas que este servidor já usa, conferidas no diagnóstico das lojas (21/11/2026). A função funciona sem
    /// "Configurar"; o superadministrador ainda pode trocar a tabela, e a configuração salva vale mais que esta.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, GameSourceBinding> BuiltIn = new Dictionary<string, GameSourceBinding>(StringComparer.Ordinal)
    {
        // Produtos do NPC Box Donate: 11 Box Donate (Spec 1..11) e 6 pacotes de KC (Spec 12..17), preço em centavos de real.
        ["box-prices"] = Built("box-prices", "NexusPKAdmin", "NexusPixProducts", ["ID"], ["Title", "PriceCents", "Enabled"], ["Title"], null, false, false, false),

        // Coin Shop (KC) com a galeria de skins: CoinShopDB.ItemList, sem chave; código + seção identificam a linha.
        ["skin-shop"] = Built("skin-shop", "CoinShopDB", "ItemList", ["szItemCode", "ItemSection"], ["Price"], ["szItemCode"], null, true, true, false),

        // Loja de minutos: mesmo formato, em TimeShopDB.
        ["time-shop"] = Built("time-shop", "TimeShopDB", "ItemList", ["szItemCode", "ItemSection"], ["Price"], ["szItemCode"], null, true, true, false),

        // Log de itens do jogo; RegistDay só ordena (mais novos primeiro).
        ["item-logs"] = Built("item-logs", "ItemLogDB", "IL", ["RegistDay"], [], ["UserID", "CharName"], "UserID", false, false, true),
    };

    private static GameSourceBinding Built(string key, string db, string table, string[] keys, string[] editable, string[] search, string? account,
        bool insert, bool delete, bool newest) =>
        new(key, db, table, keys, editable, search, account, insert, delete, newest, BuiltInAuthor, DateTimeOffset.UnixEpoch);
}

/// <summary>Coluna real de uma tabela do jogo (lida de sys.columns).</summary>
public sealed record GameColumn(string Name, string SqlType, int MaxLength, int Precision, int Scale, bool Nullable, bool Identity, bool Computed, bool HasDefault)
{
    /// <summary>Tipos que o painel sabe mostrar e converter com segurança.</summary>
    public bool Supported => GameValues.Kind(SqlType) != GameValueKind.Unsupported;

    public bool Insertable => Supported && !Identity && !Computed && SqlType != "timestamp" && SqlType != "rowversion";

    /// <summary>Máximo de caracteres (nvarchar/nchar guardam 2 bytes por caractere; -1 = max).</summary>
    public int MaxChars => MaxLength < 0 ? int.MaxValue : SqlType is "nvarchar" or "nchar" ? MaxLength / 2 : MaxLength;
}

public sealed record GameTableInfo(string Name, long Rows);

public interface IGameSchemaReader
{
    bool Enabled { get; }

    /// <summary>Bancos que o login de administração consegue abrir.</summary>
    Task<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken ct);

    Task<IReadOnlyList<GameTableInfo>> ListTablesAsync(string database, CancellationToken ct);

    /// <summary>Colunas na ordem da tabela. Vazio se a tabela não existe.</summary>
    Task<IReadOnlyList<GameColumn>> GetColumnsAsync(string database, string table, CancellationToken ct);
}

/// <summary>Ligação de uma função do painel a uma tabela real (guardada no banco do portal).</summary>
public sealed record GameSourceBinding(
    string FunctionKey,
    string Database,
    string Table,
    IReadOnlyList<string> KeyColumns,
    IReadOnlyList<string> EditableColumns,
    IReadOnlyList<string> SearchColumns,
    string? AccountColumn,
    bool AllowInsert,
    bool AllowDelete,
    bool NewestFirst,
    string UpdatedBy,
    DateTimeOffset UpdatedUtc);

public interface IGameSourceBindingStore
{
    Task<IReadOnlyList<GameSourceBinding>> ListAsync(CancellationToken ct);

    Task<GameSourceBinding?> GetAsync(string functionKey, CancellationToken ct);

    Task SaveAsync(GameSourceBinding binding, CancellationToken ct);

    Task DeleteAsync(string functionKey, CancellationToken ct);
}

/// <summary>Tabela já conferida contra o schema: banco, nome e colunas reais.</summary>
public sealed record ResolvedTable(string Database, string Table, IReadOnlyList<GameColumn> Columns, IReadOnlyList<GameColumn> KeyColumns, bool NewestFirst)
{
    public GameColumn Column(string name) => Columns.First(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}

public sealed record GameTableFilter(GameColumn Column, object Value, bool Prefix);

public sealed record GameRowPage(IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, bool HasMore);

public enum RowWriteOutcome
{
    Ok,
    NotFound,
    Ambiguous,
}

public sealed record RowWrite(RowWriteOutcome Outcome, IReadOnlyDictionary<string, object?>? Before);

/// <summary>Leitura e escrita genérica, sempre com parâmetros e só com nomes conferidos no schema.</summary>
public interface IGameTableStore
{
    Task<GameRowPage> QueryAsync(ResolvedTable table, IReadOnlyList<GameTableFilter> filters, int skip, int take, CancellationToken ct);

    /// <summary>Muda a única linha com as chaves dadas. Mais de uma linha: nada muda.</summary>
    Task<RowWrite> UpdateAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values, CancellationToken ct);

    /// <summary>
    /// Inclui uma linha. Com <paramref name="uniqueKeys"/>, recusa (false) se já existir linha com essas chaves,
    /// para a linha nova não ficar impossível de editar.
    /// </summary>
    Task<bool> InsertAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> values, IReadOnlyDictionary<string, object?>? uniqueKeys, CancellationToken ct);

    Task<RowWrite> DeleteAsync(ResolvedTable table, IReadOnlyDictionary<string, object?> keys, CancellationToken ct);
}
