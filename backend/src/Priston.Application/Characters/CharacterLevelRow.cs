namespace Priston.Application.Characters;

/// <summary>Linha pública do ranking por nível. Nunca carrega a conta (szID).</summary>
public sealed record CharacterLevelRow(string Name, int ClassCode, int Level);

public interface ILevelRankingReader
{
    /// <summary>Todos os personagens com nível &gt; 0, sem GMs. Leitura completa da LevelTable.</summary>
    Task<IReadOnlyList<CharacterLevelRow>> ReadAllAsync(CancellationToken cancellationToken);
}
