namespace Priston.GameDatabaseIntegration;

/// <summary>
/// Configuração do acesso ao banco original do jogo.
/// As connection strings vêm de variáveis de ambiente ou do repositório de segredos,
/// nunca do appsettings versionado.
/// </summary>
public sealed class GameDatabaseOptions
{
    public const string SectionName = "GameDatabase";

    /// <summary>Login SQL somente leitura (db_datareader nos bancos do jogo).</summary>
    public string ReadConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Login SQL com permissão apenas de INSERT/UPDATE nas tabelas &lt;letra&gt;GameUser.
    /// Vazio desativa toda escrita no banco do jogo.
    /// </summary>
    public string WriteConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Login SQL do painel administrativo (portal_game_admin): leitura e escrita nos bancos do jogo liberados em
    /// database/scripts/permissoes-admin-jogo.sql. Vazio desliga as funções do jogo no painel (contas, coins, VIP, editores).
    /// </summary>
    public string AdminConnectionString { get; set; } = string.Empty;

    public string AccountDatabase { get; set; } = "accountdb";

    public string ServerDatabase { get; set; } = "ServerDB";

    public string UserDatabase { get; set; } = "UserDB";

    public string ClanDatabase { get; set; } = "ClanDB";

    public string CoinShopDatabase { get; set; } = "CoinShopDB";

    /// <summary>Banco do SoD (Bellatra), com uma tabela SOD2Record&lt;aamm&gt; por mês.</summary>
    public string SodDatabase { get; set; } = "sodDB";

    /// <summary>Banco do log de itens (tabela IL), fonte dos drops raros da home.</summary>
    public string ItemLogDatabase { get; set; } = "ItemLogDB";

    /// <summary>Banco com os nomes dos tipos de item (tabela ItemType).</summary>
    public string ItemDatabase { get; set; } = "ItemDB";

    /// <summary>Banco do sistema de donate do servidor (NexusDonatePackages, NexusDonateItems, NexusDonateRuntime).</summary>
    public string DonateDatabase { get; set; } = "NexusPKAdmin";

    /// <summary>Valor de IL.Flag que o servidor grava quando o personagem pega um item do chão.</summary>
    public int ItemPickupFlag { get; set; }

    /// <summary>
    /// Valores de IL.Flag que o servidor grava num aging (histórico público "Aging"). Vazio = desligado: o significado de cada
    /// Flag muda de servidor para servidor e não está no DATABASE_MAPPING.md, então o portal não adivinha
    /// (ver <c>database/scripts/descobrir-flags-log-itens.sql</c>).
    /// </summary>
    public List<int> ItemLogAgingFlags { get; set; } = [];

    /// <summary>Valores de IL.Flag de mix/craft (histórico público "Craft"). Vazio = desligado.</summary>
    public List<int> ItemLogCraftFlags { get; set; } = [];

    public int CommandTimeoutSeconds { get; set; } = 15;
}
