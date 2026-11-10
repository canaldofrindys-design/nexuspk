using Priston.Application.Player;

namespace Priston.UnitTests;

public class PlayerPanelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_merges_clan_and_pvp_by_character_and_orders_by_level()
    {
        var raw = new RawPlayerGameData(
            new DateTime(2026, 1, 2, 10, 0, 0),
            Blocked: false,
            [new("Segundo", 7, 90), new("Primeiro", 1, 120)],
            [new("primeiro", "Clã Teste")],
            [new("Primeiro", 300, 12, 4)],
            [],
            Coins: 50);

        var game = PlayerPanelService.Build(raw, Now);

        Assert.Equal(["Primeiro", "Segundo"], game.Characters.Select(c => c.Name));
        Assert.Equal("Lutador", game.Characters[0].ClassName);
        Assert.Equal("Clã Teste", game.Characters[0].Clan);
        Assert.Equal(300, game.Characters[0].BattlePoints);
        Assert.Null(game.Characters[1].Clan);
        Assert.Null(game.Characters[1].PvpKills);
        Assert.Equal(50, game.Coins);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 13, 0, 0, TimeSpan.Zero), game.RegisteredUtc);
    }

    [Fact]
    public void Vip_prefers_the_active_plan()
    {
        var raw = new RawPlayerGameData(null, false, [], [], [],
            [new(1, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1)), new(2, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1))],
            null);

        var vip = PlayerPanelService.Build(raw, Now).Vip!;

        Assert.True(vip.Active);
        Assert.Equal(2, vip.Plan);
    }

    [Fact]
    public void Expired_vip_is_not_active()
    {
        var raw = new RawPlayerGameData(null, false, [], [], [], [new(1, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1))], null);

        Assert.False(PlayerPanelService.Build(raw, Now).Vip!.Active);
    }

    [Theory]
    [InlineData("203.0.113.9", "203.0.*.*")]
    [InlineData("2001:db8:1::5", "2001:db8:…")]
    [InlineData(null, null)]
    public void Ip_is_masked(string? ip, string? expected) => Assert.Equal(expected, PlayerPanelService.MaskIp(ip));
}
