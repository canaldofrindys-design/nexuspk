using Microsoft.Extensions.Logging;
using Priston.Application.Accounts;
using Priston.Application.Characters;
using Priston.Domain.Characters;

namespace Priston.Application.Player;

public sealed record PanelCharacter(
    string Name,
    int ClassCode,
    string ClassName,
    int Level,
    string? Clan,
    int? BattlePoints,
    int? PvpKills,
    int? PvpDeaths,
    int? RankPosition = null,
    int? ClassRankPosition = null);

public sealed record PanelVip(int? Plan, DateTimeOffset? StartsUtc, DateTimeOffset? EndsUtc, bool Active);

/// <summary>Dados da conta lidos do banco do jogo. Campos nulos: o jogo não tem o dado para esta conta.</summary>
public sealed record PlayerGameData(
    DateTimeOffset? RegisteredUtc,
    bool Blocked,
    IReadOnlyList<PanelCharacter> Characters,
    PanelVip? Vip,
    int? Coins,
    int? RankTotal = null);

public sealed record RecentLogin(DateTimeOffset AtUtc, string? IpAddress);

public sealed record PlayerPanel(
    string Login,
    string? Email,
    bool EmailConfirmed,
    bool EmailSendingAvailable,
    bool PasswordChangeAvailable,
    bool GameDataAvailable,
    PlayerGameData? Game,
    IReadOnlyList<RecentLogin> RecentLogins);

public sealed record RawCharacter(string Name, int ClassCode, int Level);

public sealed record RawClanMember(string CharacterName, string? ClanName);

public sealed record RawBattlePoints(string Nick, int? BattlePoints, int? Kills, int? Deaths);

public sealed record RawVip(int? Plan, DateTime? StartLocal, DateTime? EndLocal);

public sealed record RawPlayerGameData(
    DateTime? RegisteredLocal,
    bool Blocked,
    IReadOnlyList<RawCharacter> Characters,
    IReadOnlyList<RawClanMember> ClanMembers,
    IReadOnlyList<RawBattlePoints> BattlePoints,
    IReadOnlyList<RawVip> Vips,
    int? Coins);

/// <summary>Leitura (só leitura) dos dados de uma conta nos bancos do jogo.</summary>
public interface IPlayerGameDataReader
{
    Task<RawPlayerGameData> ReadAsync(string login, CancellationToken ct);
}

public interface IPlayerActivityReader
{
    Task<IReadOnlyList<RecentLogin>> RecentLoginsAsync(string login, int take, CancellationToken ct);
}

/// <summary>Monta o painel juntando banco do jogo (leitura) e banco do portal.</summary>
public sealed partial class PlayerPanelService(
    IPlayerGameDataReader gameData,
    IPlayerActivityReader activity,
    IPlayerAccountRepository players,
    IGameAccountStore gameAccounts,
    IEmailSender email,
    TimeProvider clock,
    ILogger<PlayerPanelService> logger,
    RankingService? ranking = null)
{
    /// <summary>Datas do jogo são gravadas no horário local do servidor (America/Sao_Paulo).</summary>
    private static readonly TimeZoneInfo GameTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<PlayerPanel> GetAsync(string login, CancellationToken ct)
    {
        var account = await players.FindAsync(login, ct);
        var logins = await activity.RecentLoginsAsync(login, 10, ct);

        PlayerGameData? game = null;
        try
        {
            game = Build(await gameData.ReadAsync(login, ct), clock.GetUtcNow());
        }
#pragma warning disable CA1031 // Sem o banco do jogo o painel abre do mesmo jeito, avisando que os dados estão indisponíveis.
        catch (Exception ex) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogGameDataFailed(logger, ex, login);
            game = null;
        }

        if (game is not null && ranking is not null)
        {
            try
            {
                game = WithPositions(game, await ranking.GetLevelRankingAsync(ct).ConfigureAwait(false));
            }
#pragma warning disable CA1031 // Ranking fora do ar: o painel abre sem a posição.
            catch (Exception ex) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
            {
                LogRankingFailed(logger, ex, login);
            }
        }

        return new PlayerPanel(
            login,
            account?.Email,
            account?.EmailConfirmedUtc is not null,
            email.IsConfigured,
            gameAccounts.WritesEnabled,
            game is not null,
            game,
            logins.Select(l => l with { IpAddress = MaskIp(l.IpAddress) }).ToList());
    }

    public static PlayerGameData Build(RawPlayerGameData raw, DateTimeOffset now)
    {
        var clans = raw.ClanMembers
            .GroupBy(m => m.CharacterName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ClanName, StringComparer.OrdinalIgnoreCase);
        var pvp = raw.BattlePoints
            .GroupBy(b => b.Nick, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var characters = raw.Characters
            .OrderByDescending(c => c.Level)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                pvp.TryGetValue(c.Name, out var bp);
                return new PanelCharacter(
                    c.Name,
                    c.ClassCode,
                    CharacterClassNames.ToPortuguese(CharacterClassNames.FromCode(c.ClassCode)),
                    c.Level,
                    clans.GetValueOrDefault(c.Name) is { Length: > 0 } clan ? clan : null,
                    bp?.BattlePoints,
                    bp?.Kills,
                    bp?.Deaths);
            })
            .ToList();

        var vip = raw.Vips
            .Select(v => new PanelVip(v.Plan, ToUtc(v.StartLocal), ToUtc(v.EndLocal), ToUtc(v.EndLocal) is { } end && end > now))
            .OrderByDescending(v => v.Active)
            .ThenByDescending(v => v.EndsUtc)
            .FirstOrDefault();

        return new PlayerGameData(ToUtc(raw.RegisteredLocal), raw.Blocked, characters, vip, raw.Coins);
    }

    /// <summary>Posição de cada personagem no ranking geral por nível e no ranking da própria classe (mesma ordem da página Ranking).</summary>
    public static PlayerGameData WithPositions(PlayerGameData game, IReadOnlyList<RankedCharacter> ranking)
    {
        var general = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var classCounters = new Dictionary<int, int>();
        foreach (var r in ranking)
        {
            general.TryAdd(r.Name, r.Position);
            var n = classCounters.GetValueOrDefault(r.ClassCode) + 1;
            classCounters[r.ClassCode] = n;
            byClass.TryAdd(r.Name, n);
        }

        var characters = game.Characters
            .Select(c => c with { RankPosition = general.TryGetValue(c.Name, out var p) ? p : null, ClassRankPosition = byClass.TryGetValue(c.Name, out var cp) ? cp : null })
            .ToList();
        return game with { Characters = characters, RankTotal = ranking.Count };
    }

    public static DateTimeOffset? ToUtc(DateTime? local)
    {
        if (local is null)
        {
            return null;
        }

        var unspecified = DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, GameTimeZone), TimeSpan.Zero);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler o ranking para o painel da conta {Login}")]
    private static partial void LogRankingFailed(ILogger logger, Exception ex, string login);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler os dados do jogo da conta {Login}")]
    private static partial void LogGameDataFailed(ILogger logger, Exception ex, string login);

    /// <summary>Mostra só o começo do IP: suficiente para o jogador reconhecer o acesso.</summary>
    public static string? MaskIp(string? ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            return null;
        }

        if (ip.Contains('.', StringComparison.Ordinal))
        {
            var parts = ip.Split('.');
            return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.*.*" : null;
        }

        var groups = ip.Split(':');
        return groups.Length >= 2 ? $"{groups[0]}:{groups[1]}:…" : null;
    }
}
