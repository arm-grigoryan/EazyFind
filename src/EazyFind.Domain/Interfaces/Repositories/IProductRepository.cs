using EazyFind.Domain.Common;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;

namespace EazyFind.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<PaginatedResult<Product>> GetPaginatedAsync(PaginationFilter paginationFilter, StoreKey? store, CategoryType? category, string searchText, CancellationToken cancellationToken);
    Task<Dictionary<string, Product>> GetByStoreAsync(StoreKey store, CancellationToken cancellationToken);
    Task<Dictionary<string, Product>> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken);
    Task BulkAddProductsAsync(List<Product> products, CancellationToken cancellationToken);
    Task BulkUpdateProductsAsync(List<Product> products, CancellationToken cancellationToken);
    Task BulkDeleteProductsAsync(List<Product> products, CancellationToken cancellationToken);
}
