using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Priston.Application.Accounts;
using Priston.Application.Server;
using Priston.Infrastructure.Accounts;
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
        services.AddSingleton<Priston.Application.Characters.ILevelSnapshotStore, LevelSnapshotStore>();
        services.AddSingleton<IPlayerAccountRepository, PlayerAccountRepository>();
        services.AddSingleton<IPlayerSessionStore, PlayerSessionStore>();
        services.AddSingleton<IOneTimeTokenStore, OneTimeTokenStore>();
        services.AddSingleton<IAuditLog, AuditLogWriter>();
        services.AddSingleton<Priston.Application.Admin.IStaffRepository, Admin.StaffRepository>();
        services.AddSingleton<Priston.Application.Admin.IStaffSessionStore, Admin.StaffSessionStore>();
        services.AddSingleton<Priston.Application.Admin.IContentAdminStore, Admin.ContentAdminStore>();
        services.AddSingleton<Priston.Application.Admin.IPortalStatsReader, Admin.PortalStatsReader>();
        services.AddSingleton<Priston.Application.Admin.IAuditReader, Admin.AuditReader>();
        services.AddSingleton<Priston.Application.Content.INewsReader, Content.NewsReader>();
        services.AddSingleton<Priston.Application.Content.IEventReader, Content.EventReader>();
        services.AddSingleton<Priston.Application.Content.IEventReminderStore, Content.EventReminderStore>();
        services.AddSingleton<Priston.Application.Player.IPlayerActivityReader, PlayerActivityReader>();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddOptions<CaptchaOptions>().Bind(configuration.GetSection(CaptchaOptions.SectionName));
        services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>(c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddHealthChecks().AddCheck<PortalDatabaseHealthCheck>(HealthCheckName, tags: ["ready"]);
        return services;
    }
}
