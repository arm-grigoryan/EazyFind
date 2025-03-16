using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Data.Repositories;

internal class ProductRepository(EazyFindDbContext dbContext) : IProductRepository
{
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
