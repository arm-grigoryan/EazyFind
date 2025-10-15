using EazyFind.Application.Alerts;
using EazyFind.Application.Category;
using EazyFind.Application.Products;
using Microsoft.Extensions.DependencyInjection;

namespace EazyFind.Application;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddOptions<AlertOptions>();

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IStoreCategoryService, StoreCategoryService>();
        services.AddScoped<IAlertService, AlertService>();

        services.AddSingleton<IProductMessageBuilder, ProductMessageBuilder>();

        return services;
    }
}
