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

    public string AccountDatabase { get; set; } = "accountdb";

    public string ServerDatabase { get; set; } = "ServerDB";

    public string UserDatabase { get; set; } = "UserDB";

    public string ClanDatabase { get; set; } = "ClanDB";

    public string CoinShopDatabase { get; set; } = "CoinShopDB";

    public int CommandTimeoutSeconds { get; set; } = 15;
}
