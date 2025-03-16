using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;

namespace EazyFind.Application.Category;

public interface IStoreCategoryService
{
    Task<StoreCategory> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken);
}
