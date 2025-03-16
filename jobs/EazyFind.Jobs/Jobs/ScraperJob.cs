using EazyFind.Application.Category;
using EazyFind.Application.Products;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Scrapers.Interfaces;
using Microsoft.Extensions.Options;

namespace EazyFind.Jobs.Jobs;

public class ScraperJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    //private readonly ILogger<ScraperJob> _logger; // TODO handle logging
    private readonly CategoryConfigs _categoryConfigs;

    public ScraperJob(
        IServiceScopeFactory scopeFactory,
        //ILogger<ScraperJob> logger,
        IOptions<CategoryConfigs> categoryConfigs)
    {
        _scopeFactory = scopeFactory;
        //_logger = logger;
        _categoryConfigs = categoryConfigs.Value;
    }

    public async Task RunScrapersAsync(CategoryType categoryType)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var productService = serviceProvider.GetRequiredService<IProductService>();
        var storeCategoryService = serviceProvider.GetRequiredService<IStoreCategoryService>();

        if (!_categoryConfigs.Categories.TryGetValue(categoryType, out var storeCategories) || storeCategories.Count == 0)
        {
            //_logger.LogInformation("No stores found for category {Category}", categoryType);
            return;
        }

        foreach (var storeCategoryConfig in storeCategories)
        {
            var scraper = serviceProvider.GetKeyedService<IScraper>(storeCategoryConfig.Store);

            //_logger.LogInformation("Running scraper for {StoreKey} - {Category} ...", storeCategory.Store, categoryType);

            var products = await scraper.ScrapeAsync(storeCategoryConfig.Url, CancellationToken.None);
            var existingProducts = await productService.GetByStoreAndCategoryAsync(storeCategoryConfig.Store, categoryType, CancellationToken.None);

            var storeCategoryEntity = await storeCategoryService.GetByStoreAndCategoryAsync(storeCategoryConfig.Store, categoryType, CancellationToken.None);

            var (newProducts, updatedProducts, productsToDelete) = ProcessProducts(products, existingProducts, storeCategoryEntity);

            if (newProducts.Count > 0)
                await productService.BulkAddProductsAsync(newProducts, CancellationToken.None);

            if (updatedProducts.Count > 0)
                await productService.BulkUpdateProductsAsync(updatedProducts, CancellationToken.None);

            if (productsToDelete.Count > 0)
                await productService.BulkDeleteProductsAsync(productsToDelete, CancellationToken.None);
        }
    }

    private static (List<Product> newProducts, List<Product> updatedProducts, List<Product> productsToDelete)
        ProcessProducts(List<Product> scrapedProducts, Dictionary<string, Product> existingProducts, StoreCategory storeCategory)
    {
        var newProducts = new List<Product>();
        var updatedProducts = new List<Product>();
        var storeCategoryId = storeCategory.Id;

        foreach (var scrapedProduct in scrapedProducts)
        {
            if (existingProducts.TryGetValue(scrapedProduct.Url, out var existingProduct))
            {
                if (existingProduct.Name != scrapedProduct.Name ||
                    existingProduct.Price != scrapedProduct.Price ||
                    existingProduct.ImageUrl != scrapedProduct.ImageUrl)
                {
                    existingProduct.Name = scrapedProduct.Name;
                    existingProduct.Price = scrapedProduct.Price;
                    existingProduct.ImageUrl = scrapedProduct.ImageUrl;
                    existingProduct.LastSyncedAt = DateTime.UtcNow;
                    updatedProducts.Add(existingProduct);
                }

                existingProducts.Remove(scrapedProduct.Url);
            }
            else
            {
                scrapedProduct.StoreCategoryId = storeCategoryId;
                newProducts.Add(scrapedProduct);
            }
        }

        var productsToDelete = existingProducts.Values.ToList();

        return (newProducts, updatedProducts, productsToDelete);
    }
}