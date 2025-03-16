using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;

namespace EazyFind.Application.Category;

public class StoreCategoryService(IStoreCategoryRepository repository) : IStoreCategoryService
{
    public Task<StoreCategory> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken)
    {
        return repository.GetByStoreAndCategoryAsync(store, category, cancellationToken);
    }
}
