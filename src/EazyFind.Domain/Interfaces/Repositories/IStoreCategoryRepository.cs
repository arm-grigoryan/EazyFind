using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;

namespace EazyFind.Domain.Interfaces.Repositories;

public interface IStoreCategoryRepository
{
    Task<StoreCategory> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken);
}
