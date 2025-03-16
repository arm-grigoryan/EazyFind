using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Data.Repositories;

internal class StoreCategoryRepository(EazyFindDbContext dbContext) : IStoreCategoryRepository
{
    public Task<StoreCategory> GetByStoreAndCategoryAsync(StoreKey store, CategoryType category, CancellationToken cancellationToken)
    {
        return dbContext.StoreCategories.FirstOrDefaultAsync(sc => sc.StoreKey == store && sc.CategoryType == category, cancellationToken);
    }
}
