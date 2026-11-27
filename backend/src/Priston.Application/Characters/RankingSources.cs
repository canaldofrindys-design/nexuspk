namespace Priston.Application.Characters;

/// <summary>Linha do ranking PvP (UserDB.dbo.BattlePoints). Sem a conta.</summary>
/// <paramref name="WeeklySod"/>: <c>SodSemanal</c>, os pontos de SoD da semana que o próprio jogo conta (zerados pelo reset do painel).
public sealed record PvpRow(string Name, int ClassCode, int BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths, bool IsPvpKing, int PvpPoints = 0, int WeeklySod = 0);

/// <summary>
/// Linha do ranking de clãs (ClanDB.dbo.CL).
/// <paramref name="IconId"/> é <c>CL.MIconCnt</c>, o número do ícone do clã, que o jogo usa para identificar o dono do Bless Castle.
/// <paramref name="Leader"/> é <c>CL.ClanZang</c>, o personagem líder (o jogo grava o nome do personagem; a conta fica em <c>CL.UserID</c>,
/// que o portal não lê). Só aparece nos destaques da home.
/// </summary>
public sealed record ClanRow(string Name, int Members, int Points, int Wins, int Losses, DateTime? CreatedLocal, int IconId = 0, string? Leader = null);

/// <summary>Melhor pontuação de um personagem no SoD do mês (sodDB.dbo.SOD2Record&lt;aamm&gt;).</summary>
public sealed record SodPlayerRow(string Name, int ClassCode, int Level, int Points);

public interface ISodRankingReader
{
    /// <summary>
    /// Os personagens que pontuaram no SoD do mês (<paramref name="month"/>, primeiro dia, em horário de Brasília), um por
    /// personagem com a maior pontuação, do maior para o menor. Sem GMs e sem a lista de escondidos do ranking.
    /// Null quando a tabela do mês não existe (o servidor do jogo ainda não a criou).
    /// </summary>
    Task<IReadOnlyList<SodPlayerRow>?> ReadMonthAsync(DateOnly month, CancellationToken ct);

    /// <summary>Os meses que têm tabela no sodDB (primeiro dia de cada um), do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<DateOnly>> ReadMonthsAsync(CancellationToken ct);
}

public interface IPvpRankingReader
{
    /// <summary>Todas as linhas de BattlePoints, sem GMs.</summary>
    Task<IReadOnlyList<PvpRow>> ReadAllAsync(CancellationToken ct);
}

public interface IClanRankingReader
{
    Task<IReadOnlyList<ClanRow>> ReadAllAsync(CancellationToken ct);
}

public interface ICharacterClanReader
{
    /// <summary>Clã de cada personagem (ClanDB.dbo.UL: ChName → ClanName).</summary>
    Task<IReadOnlyDictionary<string, string>> ReadAllAsync(CancellationToken ct);
}

/// <summary>Fotos diárias de nível guardadas no PortalDB pelo Worker.</summary>
public interface ILevelSnapshotStore
{
    Task<bool> ExistsAsync(DateOnly day, CancellationToken ct);

    Task SaveAsync(DateOnly day, DateTimeOffset takenUtc, IReadOnlyList<CharacterLevelRow> rows, CancellationToken ct);

    /// <summary>A primeira foto tirada em <paramref name="from"/> ou depois, com os níveis por personagem.</summary>
    Task<(DateOnly Date, IReadOnlyDictionary<string, int> Levels)?> FirstOnOrAfterAsync(DateOnly from, CancellationToken ct);

    Task<DateOnly?> FirstDateAsync(CancellationToken ct);

    Task<int> DeleteBeforeAsync(DateOnly day, CancellationToken ct);
}

/// <summary>
/// Dono do Bless Castle, gravado pelo servidor do jogo no arquivo <c>BlessCastle.dat</c>.
/// Retorna o número do ícone do clã (<c>CL.MIconCnt</c>) ou null quando o castelo está sem dono.
/// Lança exceção quando o arquivo não pode ser lido.
/// </summary>
public interface IBlessCastleOwnerReader
{
    Task<int?> ReadOwnerIconIdAsync(CancellationToken ct);
}
