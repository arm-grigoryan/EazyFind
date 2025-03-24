using EazyFind.Domain.Enums;
using EazyFind.Jobs.Jobs;
using EazyFind.Jobs.ScraperAPI;
using EazyFind.Jobs.Scrapers;
using EazyFind.Jobs.Scrapers.Interfaces;

namespace EazyFind.Jobs.Extensions;

public static class ScraperServiceCollectionExtensions
{
    public static IServiceCollection AddScrapers(this IServiceCollection services)
    {
        services.AddKeyedScoped<IScraper, RedStoreScraper>(StoreKey.RedStore);
        services.AddKeyedScoped<IScraper, ThreeDPlanetScraper>(StoreKey.ThreeDPlanet);
        services.AddKeyedScoped<IScraper, YerevanMobileScraper>(StoreKey.YerevanMobile);
        services.AddKeyedScoped<IScraper, ZigzagScraper>(StoreKey.Zigzag);
        services.AddKeyedScoped<IScraper, VegaScraper>(StoreKey.Vega);
        services.AddKeyedScoped<IScraper, VDComputersScraper>(StoreKey.VdComputers);
        services.AddKeyedScoped<IScraper, VLVApiScraper>(StoreKey.VLV);
        services.AddKeyedScoped<IScraper, MobileCentreScraper>(StoreKey.MobileCentre);

        return services;
    }

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(RedStoreScraper));
        services.AddHttpClient(nameof(ThreeDPlanetScraper));
        services.AddHttpClient(nameof(VegaScraper));
        services.AddHttpClient(nameof(VDComputersScraper));
        services.AddHttpClient(nameof(VLVApiScraper));
        services.AddHttpClient(nameof(MobileCentreScraper));

        return services;
    }

    public static IServiceCollection AddScraperApi(this IServiceCollection services)
    {
        services.AddHttpClient<ScraperApiAsyncClient>();
        services.AddScoped<ScraperSessionManager>();

        return services;
    }

    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddScoped<ScraperJob>();

        return services;
    }
}
