using EazyFind.Domain.Enums;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Jobs;
using EazyFind.Jobs.ScraperAPI;
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
        services.AddKeyedScoped<IScraper, VenusScraper>(StoreKey.Venus);
        services.AddKeyedScoped<IScraper, AllCellScraper>(StoreKey.AllSell);

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
        services.AddHttpClient(nameof(VenusScraper));


        services.AddHttpClient(nameof(AllCellScraper))
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var smartProxySettings = sp.GetRequiredService<IOptions<SmartProxySettings>>().Value;

                var proxyHost = smartProxySettings.Host;
                var proxyPort = smartProxySettings.Port;
                var proxyUsername = smartProxySettings.Username;
                var proxyPassword = smartProxySettings.Password;

                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}")
                    {
                        Credentials = new NetworkCredential(proxyUsername, proxyPassword)
                    },
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = (message, certChain, sslPolicyErrors, sslPolicyErrors2) => true
                };

                return handler;
            });
        services.AddHttpClient(nameof(ZigzagScraper))
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var smartProxySettings = sp.GetRequiredService<IOptions<SmartProxySettings>>().Value;

                //byte[] certBytes = Convert.FromBase64String(smartProxySettings.CertBase64);
                //var cert = new X509Certificate2(certBytes);

                var proxyHost = smartProxySettings.Host;
                var proxyPort = smartProxySettings.Port;
                var proxyUsername = smartProxySettings.Username;
                var proxyPassword = smartProxySettings.Password;

                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}")
                    {
                        Credentials = new NetworkCredential(proxyUsername, proxyPassword)
                    },
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = (message, certChain, sslPolicyErrors, sslPolicyErrors2) => true
                };

                //handler.ClientCertificates.Add(cert);

                return handler;
            });
        services.AddHttpClient(nameof(YerevanMobileScraper))
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var smartProxySettings = sp.GetRequiredService<IOptions<SmartProxySettings>>().Value;

                //byte[] certBytes = Convert.FromBase64String(smartProxySettings.CertBase64);
                //var cert = new X509Certificate2(certBytes);

                var proxyHost = smartProxySettings.Host;
                var proxyPort = smartProxySettings.Port;
                var proxyUsername = smartProxySettings.Username;
                var proxyPassword = smartProxySettings.Password;

                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}")
                    {
                        Credentials = new NetworkCredential(proxyUsername, proxyPassword)
                    },
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = (message, certChain, sslPolicyErrors, sslPolicyErrors2) => true
                };

                //handler.ClientCertificates.Add(cert);

                return handler;
            });

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
        services.AddScoped<ICategoryInferrer, CategoryInferrer>();

        return services;
    }
}
