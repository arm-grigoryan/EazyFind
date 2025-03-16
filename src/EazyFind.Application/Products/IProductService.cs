using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;

namespace EazyFind.Application.Products;

public interface IProductService
{
    Task<Dictionary<string, Product>> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken);
    Task BulkAddProductsAsync(List<Product> products, CancellationToken cancellationToken);
    Task BulkUpdateProductsAsync(List<Product> products, CancellationToken cancellationToken);
    Task BulkDeleteProductsAsync(List<Product> products, CancellationToken cancellationToken);
}
