using Priston.Domain.Time;

namespace Priston.UnitTests;

public class RankingPeriodsTests
{
    private readonly RankingPeriods _periods = new();

    [Fact]
    public void Daily_period_starts_at_midnight_in_Sao_Paulo()
    {
        // 10/11/2026 01:30 UTC = 09/11/2026 22:30 em São Paulo (UTC-3).
        var (start, end) = _periods.GetPeriod(RankingPeriod.Daily, new DateTimeOffset(2026, 11, 10, 1, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 3, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 3, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void Weekly_period_starts_on_monday()
    {
        // Domingo 15/11/2026 12:00 local → semana de segunda 09/11 a segunda 16/11.
        var (start, end) = _periods.GetPeriod(RankingPeriod.Weekly, new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 3, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void Monthly_period_covers_the_local_calendar_month()
    {
        // 01/12/2026 02:00 UTC ainda é 30/11 em São Paulo.
        var (start, end) = _periods.GetPeriod(RankingPeriod.Monthly, new DateTimeOffset(2026, 12, 1, 2, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 3, 0, 0, TimeSpan.Zero), end);
    }
}
