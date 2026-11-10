using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Priston.Application.Accounts;
using Priston.Application.Characters;
using Priston.Application.Home;

namespace Priston.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddOptions<ServerPresentationOptions>().Bind(configuration.GetSection(ServerPresentationOptions.SectionName));
        services.AddSingleton<RankingService>();
        services.AddSingleton<HomeService>();
        services.AddSingleton<RankingQueryService>();
        services.AddSingleton<LevelSnapshotService>();
        services.AddOptions<AccountOptions>()
            .Bind(configuration.GetSection(AccountOptions.SectionName))
            .Validate(o => o.LoginMinLength >= 1 && o.LoginMaxLength <= 32 && o.LoginMinLength <= o.LoginMaxLength, "Accounts: limites de login inválidos (1–32).")
            .Validate(o => o.PasswordMinLength >= 4 && o.PasswordMaxLength <= 32 && o.PasswordMinLength <= o.PasswordMaxLength, "Accounts: limites de senha inválidos (4–32).")
            .Validate(o => Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out _), "Accounts:PublicBaseUrl deve ser uma URL absoluta.")
            .ValidateOnStart();
        services.AddSingleton<LoginThrottle>();
        services.AddScoped<AuthService>();
        services.AddScoped<Player.PlayerPanelService>();
        return services;
    }
}
