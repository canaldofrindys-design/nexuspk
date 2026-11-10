using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        return services;
    }
}
