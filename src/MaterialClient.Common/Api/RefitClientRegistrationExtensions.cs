using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Refit;

namespace MaterialClient.Common.Api;

/// <summary>
///     Registers MaterialClient Refit HTTP API clients (shared by MaterialClient and MaterialClient.Urban).
/// </summary>
public static class RefitClientRegistrationExtensions
{
    public static IServiceCollection AddMaterialClientRefitClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var basePlatformUrl = configuration["BasePlatform:BaseUrl"]
                              ?? "http://localhost:5000";

        services.AddRefitClient<IBasePlatformApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(basePlatformUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        var materialPlatformUrl = configuration["MaterialPlatform:BaseUrl"]
                                  ?? basePlatformUrl;

        services.AddTransient<MaterialPlatformBearerTokenHandler>();

        services.AddRefitClient<IMaterialPlatformApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(materialPlatformUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<MaterialPlatformBearerTokenHandler>()
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        return services;
    }
}
