using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;

namespace EazyFind.Application.Products;

internal class ProductService(IProductRepository repository) : IProductService
{
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
