namespace Priston.Domain.Time;

public enum RankingPeriod
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>
/// Calcula os limites dos períodos de ranking no fuso America/Sao_Paulo.
/// A semana começa na segunda-feira às 00:00 locais.
/// Todos os valores de entrada e saída são UTC.
/// </summary>
public sealed class RankingPeriods
{
    public const string TimeZoneId = "America/Sao_Paulo";

    private readonly TimeZoneInfo _zone;

    public RankingPeriods()
        : this(TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId))
    {
    }

    public RankingPeriods(TimeZoneInfo zone)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    public (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetPeriod(RankingPeriod period, DateTimeOffset instantUtc)
    {
        var local = TimeZoneInfo.ConvertTime(instantUtc, _zone).DateTime;
        var startLocal = period switch
        {
            RankingPeriod.Daily => local.Date,
            RankingPeriod.Weekly => local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7)),
            RankingPeriod.Monthly => new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };
        var endLocal = period switch
        {
            RankingPeriod.Daily => startLocal.AddDays(1),
            RankingPeriod.Weekly => startLocal.AddDays(7),
            _ => startLocal.AddMonths(1),
        };
        return (ToUtc(startLocal), ToUtc(endLocal));
    }

    private DateTimeOffset ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, _zone), TimeSpan.Zero);
    }
}
