using EazyFind.Application.Alerts;
using EazyFind.Domain.Interfaces.Repositories;
using EazyFind.Infrastructure.Data;
using EazyFind.Infrastructure.Data.Repositories;
using EazyFind.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EazyFind.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<EazyFindDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("EazyFindDatabase"))
               .UseSnakeCaseNamingConvention());

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStoreCategoryRepository, StoreCategoryRepository>();
        services.AddScoped<IProductAlertRepository, ProductAlertRepository>();
        services.AddScoped<IProductAlertMatchRepository, ProductAlertMatchRepository>();
        services.AddScoped<IAlertEvaluationService, AlertEvaluationService>();

        return services;
    }
}
