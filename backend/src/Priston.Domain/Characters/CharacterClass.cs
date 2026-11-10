namespace Priston.Domain.Characters;

/// <summary>
/// Códigos de classe gravados em <c>ServerDB.dbo.LevelTable.Class</c>.
/// Nomes confirmados no site antigo (DATABASE_MAPPING.md, seção 3).
/// </summary>
public enum CharacterClass
{
    Unknown = 0,
    Fighter = 1,
    Mechanician = 2,
    Archer = 3,
    Pikeman = 4,
    Atalanta = 5,
    Knight = 6,
    Magician = 7,
    Priestess = 8,
    Assassin = 9,
    Shaman = 10,
    Warrior = 11,
}

public static class CharacterClassNames
{
    private static readonly Dictionary<CharacterClass, string> PtBr = new()
    {
        [CharacterClass.Fighter] = "Lutador",
        [CharacterClass.Mechanician] = "Mecânico",
        [CharacterClass.Archer] = "Arqueira",
        [CharacterClass.Pikeman] = "Pikeman",
        [CharacterClass.Atalanta] = "Atalanta",
        [CharacterClass.Knight] = "Cavaleiro",
        [CharacterClass.Magician] = "Mago",
        [CharacterClass.Priestess] = "Sacerdotisa",
        [CharacterClass.Assassin] = "Assassina",
        [CharacterClass.Shaman] = "Xamã",
        [CharacterClass.Warrior] = "Guerreira",
    };

    public static CharacterClass FromCode(int code) =>
        Enum.IsDefined(typeof(CharacterClass), code) ? (CharacterClass)code : CharacterClass.Unknown;

    public static string ToPortuguese(CharacterClass value) =>
        PtBr.TryGetValue(value, out var name) ? name : "Desconhecida";
}
