using Priston.Domain.Characters;

namespace Priston.UnitTests;

public class CharacterClassTests
{
    [Theory]
    [InlineData(1, "Lutador")]
    [InlineData(8, "Sacerdotisa")]
    [InlineData(11, "Guerreira")]
    [InlineData(0, "Desconhecida")]
    [InlineData(99, "Desconhecida")]
    public void Maps_LevelTable_codes_to_names(int code, string expected) =>
        Assert.Equal(expected, CharacterClassNames.ToPortuguese(CharacterClassNames.FromCode(code)));
}
