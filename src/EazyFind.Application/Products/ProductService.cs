using EazyFind.Domain.Common;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;

namespace EazyFind.Application.Products;

internal class ProductService(IProductRepository repository) : IProductService
{
    public Task<PaginatedResult<Product>> GetPaginatedAsync(PaginationFilter paginationFilter, StoreKey? store, CategoryType? category, string searchText, CancellationToken cancellationToken)
    {
        return repository.GetPaginatedAsync(paginationFilter, store, category, searchText, cancellationToken);
    }

    public Task<Dictionary<string, Product>> GetByStoreAsync(StoreKey store, CancellationToken cancellationToken)
    {
        return repository.GetByStoreAsync(store, cancellationToken);
    }

    public Task<Dictionary<string, Product>> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken)
    {
        return repository.GetByStoreAndCategoryAsync(store, category, cancellationToken);
    }

    public Task BulkAddProductsAsync(List<Product> products, CancellationToken cancellationToken)
    {
        return repository.BulkAddProductsAsync(products, cancellationToken);
    }

    public Task BulkUpdateProductsAsync(List<Product> products, CancellationToken cancellationToken)
    {
        return repository.BulkUpdateProductsAsync(products, cancellationToken);
    }

    public Task BulkDeleteProductsAsync(List<Product> products, CancellationToken cancellationToken)
    {
        return repository.BulkDeleteProductsAsync(products, cancellationToken);
    }
}
