using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Priston.Application.Accounts;
using Priston.Application.Characters;
using Priston.GameDatabaseIntegration.Accounts;
using Priston.GameDatabaseIntegration.Characters;
using Priston.GameDatabaseIntegration.Connection;
using Priston.GameDatabaseIntegration.Health;

namespace Priston.GameDatabaseIntegration;

public static class DependencyInjection
{
    public const string HealthCheckName = "game-db";

    public static IServiceCollection AddGameDatabaseIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GameDatabaseOptions>()
            .Bind(configuration.GetSection(GameDatabaseOptions.SectionName))
            .Validate(o => o.CommandTimeoutSeconds is > 0 and <= 120, "CommandTimeoutSeconds deve estar entre 1 e 120.")
            .ValidateOnStart();

        services.AddSingleton<Priston.Application.Admin.IGameStatsReader, Admin.GameStatsReader>();
        services.AddSingleton<IGameDbConnectionFactory, GameDbConnectionFactory>();
        services.AddSingleton<ILevelRankingReader, LevelRankingReader>();
        services.AddSingleton<IPvpRankingReader, PvpRankingReader>();
        services.AddSingleton<IClanRankingReader, ClanRankingReader>();
        services.AddSingleton<ISodRankingReader, SodRankingReader>();
        services.AddSingleton<ICharacterClanReader, CharacterClanReader>();
        services.AddSingleton<Priston.Application.Home.IItemLogReader, ItemLogReader>();
        services.AddSingleton<Priston.Application.Home.IItemNameReader, ItemTypeNameReader>();
        services.AddSingleton<IGameAccountStore, GameAccountStore>();
        services.AddSingleton<Priston.Application.Player.IPlayerGameDataReader, Player.PlayerGameDataReader>();
        services.AddSingleton<Priston.Application.GameAdmin.IGameAdminAccountStore, Admin.GameAdminAccountStore>();
        services.AddSingleton<Priston.Application.GameAdmin.IRankingResetStore, Admin.RankingResetStore>();
        services.AddSingleton<Priston.Application.GameAdmin.IGameSchemaReader, Admin.GameSchemaReader>();
        services.AddSingleton<Priston.Application.GameAdmin.IGameTableStore, Admin.GameTableStore>();
        services.AddSingleton<Priston.Application.GameAdmin.IDonateBoxStore, Admin.DonateBoxStore>();
        services.AddHealthChecks().AddCheck<GameDatabaseHealthCheck>(HealthCheckName, tags: ["ready"]);
        return services;
    }
}
