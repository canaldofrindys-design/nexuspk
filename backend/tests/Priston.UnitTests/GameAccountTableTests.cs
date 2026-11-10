using Priston.GameDatabaseIntegration;
using Priston.GameDatabaseIntegration.Accounts;

namespace Priston.UnitTests;

public class GameAccountTableTests
{
    [Theory]
    [InlineData("alpha", "[accountdb].[dbo].[AGameUser]")]
    [InlineData("Delta1", "[accountdb].[dbo].[DGameUser]")]
    [InlineData("november", "[accountdb].[dbo].[NGameUser]")]
    [InlineData("9lives", "[accountdb].[dbo].[9GameUser]")]
    public void Resolves_shard_from_first_character(string login, string expected) =>
        Assert.Equal(expected, GameAccountTable.QualifiedNameFor(login, "accountdb"));

    [Theory]
    [InlineData("")]
    [InlineData(" alpha")]
    [InlineData("_admin")]
    [InlineData("-admin")]
    [InlineData("adm pk")]
    [InlineData("a'; DROP TABLE x--")]
    [InlineData("çonta")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")] // 33 caracteres
    public void Rejects_invalid_logins(string login)
    {
        Assert.False(GameAccountTable.IsValidLogin(login));
        Assert.Throws<ArgumentException>(() => GameAccountTable.QualifiedNameFor(login, "accountdb"));
    }

    [Fact]
    public void Accepts_32_character_login() =>
        Assert.True(GameAccountTable.IsValidLogin(new string('a', 32)));

    [Fact]
    public void Has_36_shards_matching_the_database() =>
        Assert.Equal("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", new string(GameAccountTable.AllShards.ToArray()));

    [Theory]
    [InlineData("account db")]
    [InlineData("accountdb]; --")]
    [InlineData("")]
    public void Rejects_unsafe_database_names(string database) =>
        Assert.Throws<ArgumentException>(() => SqlIdentifier.Quote(database));
}
