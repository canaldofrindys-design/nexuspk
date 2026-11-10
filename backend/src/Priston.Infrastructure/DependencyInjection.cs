using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Priston.Application.Server;
using Priston.Domain.Time;
using Priston.Infrastructure.GameServer;
using Priston.Infrastructure.Persistence;

namespace Priston.Infrastructure;

public static class DependencyInjection
{
    public const string HealthCheckName = "portal-db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PortalDatabaseOptions>()
            .Bind(configuration.GetSection(PortalDatabaseOptions.SectionName))
            .Validate(o => o.CommandTimeoutSeconds is > 0 and <= 300, "CommandTimeoutSeconds deve estar entre 1 e 300.")
            .ValidateOnStart();

        services.AddSingleton<IPortalDbConnectionFactory, PortalDbConnectionFactory>();
        services.AddOptions<GameServerOptions>().Bind(configuration.GetSection(GameServerOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IServerStatusProvider, GameServerProbe>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RankingPeriods>();
        services.AddHealthChecks().AddCheck<PortalDatabaseHealthCheck>(HealthCheckName, tags: ["ready"]);
        return services;
    }
}
