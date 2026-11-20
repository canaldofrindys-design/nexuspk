using Priston.Application.Characters;
using Priston.Domain.Characters;

namespace Priston.UnitTests;

public class RankingExclusionsTests
{
    private static readonly RankingExclusions List = new(["drakken1"], ["VINICIUSADM"], ["[ADM]", "[STAFF]"]);

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Hides_unknown_classes(int classCode)
    {
        Assert.False(CharacterClassNames.IsKnown(classCode));
        Assert.True(RankingExclusions.None.Hides("jogador", "Fulano", classCode));
    }

    [Fact]
    public void Shows_the_eleven_classes()
    {
        for (var code = 1; code <= 11; code++)
        {
            Assert.True(CharacterClassNames.IsKnown(code));
            Assert.False(List.Hides("jogador", "Fulano", code));
        }
    }

    [Theory]
    [InlineData("DRAKKEN1", "QualquerNome")]
    [InlineData(" drakken1 ", "OutroNome")]
    [InlineData("outra", "viniciusadm")]
    [InlineData("outra", "[ADM]POSEIDON")]
    [InlineData("outra", "[staff]Pkzao")]
    [InlineData("outra", "  ")]
    public void Hides_listed_accounts_characters_and_prefixes(string account, string name) =>
        Assert.True(List.Hides(account, name, 1));

    [Theory]
    [InlineData("drakken10", "Fulano")]
    [InlineData("outra", "ADAM")]
    [InlineData("outra", "Pkzao[ADM]")]
    [InlineData(null, "Fulano")]
    public void Keeps_everyone_else(string? account, string name) =>
        Assert.False(List.Hides(account, name, 6));
}
