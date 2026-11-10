using Priston.Application.Accounts;
using Priston.GameDatabaseIntegration.Accounts;

namespace Priston.UnitTests;

public class AccountRulesTests
{
    private static readonly AccountOptions Defaults = new();

    [Theory]
    [InlineData("alpha01", true)]
    [InlineData("Delta_9", true)]
    [InlineData("abc", false)]
    [InlineData("_alpha", false)]
    [InlineData("alpha 01", false)]
    [InlineData("alpha01alpha01alpha", false)]
    [InlineData("alpha'--", false)]
    public void Login_rules(string login, bool valid) =>
        Assert.Equal(valid, AccountRules.ValidateLogin(login, Defaults) is null);

    [Theory]
    [InlineData("senha123", true)]
    [InlineData("12345", false)]
    [InlineData("aaaaaaaa", false)]
    [InlineData("senha 123", false)]
    [InlineData("senha'123", false)]
    [InlineData("xalpha01x", false)]
    public void Password_rules(string password, bool valid) =>
        Assert.Equal(valid, AccountRules.ValidatePassword(password, "alpha01", Defaults) is null);

    [Theory]
    [InlineData("jogador@example.com", true)]
    [InlineData("jogador@localhost", false)]
    [InlineData("Nome <jogador@example.com>", false)]
    [InlineData("", false)]
    public void Email_rules(string email, bool valid) =>
        Assert.Equal(valid, AccountRules.ValidateEmail(email) is null);

    [Fact]
    public void Tokens_are_unique_and_hashed()
    {
        var (a, hashA) = SecureToken.Create();
        var (b, _) = SecureToken.Create();

        Assert.NotEqual(a, b);
        Assert.True(SecureToken.LooksValid(a));
        Assert.Equal(32, hashA.Length);
        Assert.Equal(hashA, SecureToken.Hash(a));
        Assert.False(SecureToken.LooksValid("curto"));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData(" ", false)]
    [InlineData(null, false)]
    [InlineData("1", true)]
    public void Game_flags(string? flag, bool set) => Assert.Equal(set, GameAccountStore.IsSet(flag));

    [Fact]
    public void Password_comparison_is_exact()
    {
        Assert.True(GameAccountStore.FixedTimeEquals("Senha123", "Senha123"));
        Assert.False(GameAccountStore.FixedTimeEquals("Senha123", "senha123"));
        Assert.False(GameAccountStore.FixedTimeEquals("Senha123", "Senha1234"));
    }
}
