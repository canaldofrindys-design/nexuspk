namespace Priston.Application.Characters;

/// <summary>Linha do ranking PvP (UserDB.dbo.BattlePoints). Sem a conta.</summary>
public sealed record PvpRow(string Name, int ClassCode, int BattlePoints, int WeeklyBattlePoints, int Kills, int Deaths, bool IsPvpKing);

/// <summary>Linha do ranking de clãs (ClanDB.dbo.CL). Sem o líder, que pode ser uma conta.</summary>
public sealed record ClanRow(string Name, int Members, int Points, int Wins, int Losses, DateTime? CreatedLocal);

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
