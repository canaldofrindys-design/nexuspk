using Priston.Application.Admin;

namespace Priston.Application.GameAdmin;

public sealed record GameAccountRow(string Login, DateTime? RegistDay, DateTime? LastDay, string? BlockChk, string? DelChk, string? Grade);

public sealed record GameCharacter(string Name, int? Class, int? Level);

/// <summary>Linha de [ServerDB].dbo.Vip. A tabela não tem chave: a linha é identificada por todos os valores.</summary>
public sealed record GameVipRow(string? PlayerName, int? Plan, DateTime? StartLocal, DateTime? EndLocal);

public sealed record GameClanRow(string? ClanName, string CharacterName, string? Permission, string? DelActive);

public sealed record GamePvpRow(string? Nick, int? BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths);

/// <summary>Tudo o que o jogo guarda sobre uma conta, lido das tabelas mapeadas em DATABASE_MAPPING.md.</summary>
public sealed record GameAccountSnapshot(
    IReadOnlyList<GameAccountRow> AccountRows,
    IReadOnlyList<GameCharacter> Characters,
    IReadOnlyList<GameVipRow> Vips,
    IReadOnlyList<int?> CoinRows,
    int? GameMasterLevel,
    IReadOnlyList<GameClanRow> Clans,
    IReadOnlyList<GamePvpRow> Pvp,
    IReadOnlyList<string> CharactersSharedWithOtherAccounts);

public sealed record AccountSearchHit(string Login, string? MatchedCharacter, bool Blocked, bool Deleted, DateTime? RegistDay);

public enum CoinOutcome
{
    Ok,

    /// <summary>Mais de uma linha da conta em CoinShopDB.UserInfo: o painel não escolhe qual mudar.</summary>
    Duplicate,

    /// <summary>A conta deixaria de ter um saldo válido (negativo ou acima do limite).</summary>
    Rejected,
}

public sealed record CoinChange(CoinOutcome Outcome, long Before, long After, bool Created);

/// <summary>
/// Leitura e escrita administrativa no banco do jogo, com o login de administração (GameDatabase:AdminConnectionString).
/// Só usa tabelas e colunas confirmadas: &lt;letra&gt;GameUser, LevelTable, Vip, CoinShopDB.UserInfo, GameMasters, UL, BattlePoints.
/// </summary>
public interface IGameAdminAccountStore
{
    bool Enabled { get; }

    Task<IReadOnlyList<AccountSearchHit>> SearchAsync(string term, int take, CancellationToken ct);

    /// <summary>Null quando a conta não existe no shard.</summary>
    Task<GameAccountSnapshot?> GetAsync(string login, CancellationToken ct);

    /// <summary>Marca ou tira BlockChk. Devolve o número de linhas alteradas.</summary>
    Task<int> SetBlockedAsync(string login, bool blocked, CancellationToken ct);

    /// <summary>Muda o saldo numa transação com trava. <paramref name="compute"/> recebe o saldo atual e devolve o novo (null recusa).</summary>
    Task<CoinChange> ChangeCoinsAsync(string login, Func<long, long?> compute, CancellationToken ct);

    Task AddVipAsync(string login, string? playerName, int plan, DateTime startLocal, DateTime endLocal, CancellationToken ct);

    /// <summary>Muda a linha que tem exatamente os valores de <paramref name="original"/>. Devolve as linhas afetadas (0 ou 1; mais de uma é desfeito).</summary>
    Task<int> UpdateVipAsync(string login, GameVipRow original, int plan, DateTime startLocal, DateTime endLocal, CancellationToken ct);

    Task<int> DeleteVipAsync(string login, GameVipRow original, CancellationToken ct);
}

public sealed record GameBanRecord(long Id, string GameLogin, string Reason, string BannedBy, DateTimeOffset BannedUtc, DateTimeOffset? ExpiresUtc,
    DateTimeOffset? LiftedUtc, string? LiftedBy, string? LiftReason);

/// <summary>Histórico de banimentos no banco do portal (o jogo só guarda BlockChk).</summary>
public interface IGameBanStore
{
    Task<long> CreateAsync(string login, string reason, string bannedBy, DateTimeOffset now, DateTimeOffset? expiresUtc, CancellationToken ct);

    Task<IReadOnlyList<GameBanRecord>> ListAsync(string login, CancellationToken ct);

    /// <summary>Encerra os banimentos ativos da conta.</summary>
    Task LiftActiveAsync(string login, string liftedBy, string reason, DateTimeOffset now, CancellationToken ct);

    Task<IReadOnlyList<GameBanRecord>> ListExpiredActiveAsync(DateTimeOffset now, CancellationToken ct);
}

/// <summary>O que o portal sabe da conta: cadastro web, sessões e auditoria.</summary>
public interface IAccountActivityReader
{
    Task<IReadOnlyList<AuditEntry>> ListForAccountAsync(string login, int take, CancellationToken ct);

    Task<int> CountActivePlayerSessionsAsync(string login, DateTimeOffset now, CancellationToken ct);
}
