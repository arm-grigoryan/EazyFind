using EazyFind.Domain.Common;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;
using EazyFind.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Data.Repositories;

internal class ProductRepository(EazyFindDbContext dbContext) : IProductRepository
{
    public async Task<PaginatedResult<Product>> GetPaginatedAsync(
        PaginationFilter paginationFilter,
        List<StoreKey> stores,
        List<CategoryType> categories,
        string searchText,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.StoreCategory)
                .ThenInclude(sc => sc.Store)
            .Include(p => p.StoreCategory)
                .ThenInclude(sc => sc.Category)
            .Where(p => !p.IsDeleted)
            .WhereIf(stores is { Count: > 0 }, p => stores.Contains(p.StoreCategory.StoreKey))
            .WhereIf(categories is { Count: > 0 }, p => categories.Contains(p.StoreCategory.CategoryType));

        if (!string.IsNullOrEmpty(searchText))
        {
            const double threshold = 0.3;
            query = query
                .Where(p => EF.Functions.TrigramsSimilarity(p.Name, searchText) > threshold)
                .OrderByDescending(p => EF.Functions.TrigramsSimilarity(p.Name, searchText));
        }
        else
        {
            query = query
                .OrderByDescending(p => p.LastSyncedAt)
                .ThenBy(p => p.Name);
        }

        return await query.PageAsync(paginationFilter, cancellationToken);
    }

    public Task<Dictionary<string, Product>> GetByStoreAsync(StoreKey store, CancellationToken cancellationToken)
    {
        return dbContext.Products.AsNoTracking()
            .Where(p => p.StoreCategory.StoreKey == store)
            .Include(p => p.StoreCategory)
            .ToDictionaryAsync(p => p.Url, cancellationToken);
    }

    public Task<Dictionary<string, Product>> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken)
    {
        return dbContext.Products.AsNoTracking()
            .Where(p => p.StoreCategory.StoreKey == store && p.StoreCategory.CategoryType == category)
            .ToDictionaryAsync(p => p.Url, cancellationToken);
    }

    public async Task BulkAddProductsAsync(List<Product> products, CancellationToken cancellationToken)
    {
        await dbContext.Products.AddRangeAsync(products, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task BulkUpdateProductsAsync(List<Product> products, CancellationToken cancellationToken)
    {
        foreach (var product in products)
        {
            dbContext.Products.Entry(product).State = EntityState.Modified;
        }
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task BulkDeleteProductsAsync(List<Product> products, CancellationToken cancellationToken)
    {
        var ids = products.Select(p => p.Id);
        return dbContext.Products
            .Where(e => ids.Contains(e.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
