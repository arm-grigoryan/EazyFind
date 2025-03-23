using EazyFind.Domain.Enums;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Jobs;
using EazyFind.Jobs.Scrapers;
using EazyFind.Jobs.Scrapers.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;

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

        services.AddHttpClient(nameof(ZigzagScraper))
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var apiKey = sp.GetRequiredService<IOptions<ScraperApiSettings>>().Value.ApiKey;
                var session = sp.GetRequiredService<IScraperSessionManager>().GetSessionNumber(nameof(ZigzagScraper));

                var proxyUsername = $"scraperapi.session_number={session}.render=true";
                var proxyPassword = apiKey;
                var proxyHost = "proxy-server.scraperapi.com";
                var proxyPort = 8001;

                return new HttpClientHandler
                {
                    Proxy = new WebProxy
                    {
                        Address = new Uri($"http://{proxyHost}:{proxyPort}"),
                        Credentials = new NetworkCredential(proxyUsername, proxyPassword)
                    },
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            });
        services.AddHttpClient(nameof(YerevanMobileScraper))
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var apiKey = sp.GetRequiredService<IOptions<ScraperApiSettings>>().Value.ApiKey;
                var session = sp.GetRequiredService<IScraperSessionManager>().GetSessionNumber(nameof(ZigzagScraper));

                var proxyUsername = $"scraperapi.session_number={session}.render=true";
                var proxyPassword = apiKey;
                var proxyHost = "proxy-server.scraperapi.com";
                var proxyPort = 8001;

                return new HttpClientHandler
                {
                    Proxy = new WebProxy
                    {
                        Address = new Uri($"http://{proxyHost}:{proxyPort}"),
                        Credentials = new NetworkCredential(proxyUsername, proxyPassword)
                    },
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            });

        return services;
    }

    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddScoped<ScraperJob>();
        services.AddScoped<IScraperSessionManager, ScraperSessionManager>();

        return services;
    }
}
