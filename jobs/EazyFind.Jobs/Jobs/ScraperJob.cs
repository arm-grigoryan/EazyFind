using EazyFind.Application.Category;
using EazyFind.Application.Products;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Scrapers.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EazyFind.Jobs.Jobs;

public class ScraperJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScraperJob> _logger;
    private readonly CategoryConfigs _categoryConfigs;

    public ScraperJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ScraperJob> logger,
        IOptions<CategoryConfigs> categoryConfigs)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _categoryConfigs = categoryConfigs.Value;
    }

    public async Task RunScrapersAsync(CategoryType categoryType)
    {
        if (!_categoryConfigs.Categories.TryGetValue(categoryType, out var storeCategories) || storeCategories.Count == 0)
        {
            _logger.LogInformation("No stores found for category {Category}", categoryType);
            return;
        }

        await Parallel.ForEachAsync(storeCategories, async (storeCategoryConfig, token) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var productService = serviceProvider.GetRequiredService<IProductService>();
            var storeCategoryService = serviceProvider.GetRequiredService<IStoreCategoryService>();
            var scraper = serviceProvider.GetKeyedService<IScraper>(storeCategoryConfig.Store);

            _logger.LogInformation("Running scraper for {StoreKey} - {Category} ...", storeCategoryConfig.Store, categoryType);

            var products = await scraper.ScrapeAsync(storeCategoryConfig.Url, token);
            var existingProducts = await productService.GetByStoreAndCategoryAsync(storeCategoryConfig.Store, categoryType, token);

            var storeCategoryEntity = await storeCategoryService.GetByStoreAndCategoryAsync(storeCategoryConfig.Store, categoryType, token);

            var (newProducts, updatedProducts, productsToDelete) = ProcessProducts(products, existingProducts, storeCategoryEntity);

            _logger.LogInformation("Processed Products for {StoreKey} {Category} {Scraper}: New: {NewProducts}, Updated: {UpdatedProducts}, Deleted: {DeletedProducts}",
                storeCategoryConfig.Store, categoryType, scraper.GetType().Name, newProducts.Count, updatedProducts.Count, productsToDelete.Count);

            if (newProducts.Count > 0)
            {
                try
                {
                    await productService.BulkAddProductsAsync(newProducts, token);
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    _logger.LogWarning(pgEx, "Duplicate product detected! Store: {Store}, Category: {Category}",
                        storeCategoryConfig.Store, categoryType);
                }
            }

            if (updatedProducts.Count > 0)
                await productService.BulkUpdateProductsAsync(updatedProducts, token);

            if (productsToDelete.Count > 0)
                await productService.BulkDeleteProductsAsync(productsToDelete, token);
        });
    }

    private (List<Product> newProducts, List<Product> updatedProducts, List<Product> productsToDelete)
        ProcessProducts(List<Product> scrapedProducts, Dictionary<string, Product> existingProducts, StoreCategory storeCategory)
    {
        var newProducts = new List<Product>();
        var updatedProducts = new List<Product>();
        var storeCategoryId = storeCategory.Id;

        foreach (var scrapedProduct in scrapedProducts)
        {
            if (existingProducts.TryGetValue(scrapedProduct.Url, out var existingProduct))
            {
                if (existingProduct.StoreCategoryId != storeCategoryId)
                {
                    _logger.LogInformation(
                        "Duplicate product detected! Store: {Store}, Existing category: {ExistingCategory}, Fetched from category: {NewCategory}, Product: {Url}",
                        storeCategory.StoreKey, existingProduct.StoreCategory.CategoryType, storeCategory.CategoryType, existingProduct.Url);
                }

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