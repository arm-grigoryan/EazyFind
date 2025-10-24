using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Extensions;
using EazyFind.Infrastructure.Data;
using EazyFind.Jobs.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EazyFind.Jobs.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var dbContext = serviceProvider.GetRequiredService<EazyFindDbContext>();
        var options = serviceProvider.GetRequiredService<IOptions<CategoryConfigs>>();
        var categoryConfigs = options.Value?.Categories ?? new Dictionary<CategoryType, List<StoreCategoryConfig>>();

        if (categoryConfigs.Count == 0)
        {
            return;
        }

        var existingStoreKeys = await dbContext.Stores
            .AsNoTracking()
            .Select(store => store.Key)
            .ToListAsync(cancellationToken);
        var existingCategoryTypes = await dbContext.Categories
            .AsNoTracking()
            .Select(category => category.Type)
            .ToListAsync(cancellationToken);
        var existingStoreCategoryPairs = await dbContext.StoreCategories
            .AsNoTracking()
            .Select(storeCategory => new { storeCategory.StoreKey, storeCategory.CategoryType })
            .ToListAsync(cancellationToken);

        var storeKeys = new HashSet<StoreKey>(existingStoreKeys);
        var categoryTypes = new HashSet<CategoryType>(existingCategoryTypes);
        var storeCategoryPairs = new HashSet<(StoreKey StoreKey, CategoryType CategoryType)>(
            existingStoreCategoryPairs.Select(pair => (pair.StoreKey, pair.CategoryType)));

        var categoriesToAdd = new List<Category>();
        var storesToAdd = new List<Store>();
        var storeCategoriesToAdd = new List<StoreCategory>();

        foreach (var (categoryType, storeConfigs) in categoryConfigs)
        {
            if (categoryTypes.Add(categoryType))
            {
                categoriesToAdd.Add(new Category
                {
                    Type = categoryType
                });
            }

            if (storeConfigs is null)
            {
                continue;
            }

            foreach (var storeConfig in storeConfigs)
            {
                if (storeConfig is null)
                {
                    continue;
                }

                var storeKey = storeConfig.Store;
                if (storeKeys.Add(storeKey))
                {
                    storesToAdd.Add(new Store
                    {
                        Key = storeKey,
                        Name = storeKey.ToDisplayName(),
                        WebsiteUrl = ExtractWebsiteUrl(storeConfig.Url)
                    });
                }

                var pairKey = (storeKey, categoryType);
                if (storeCategoryPairs.Add(pairKey))
                {
                    storeCategoriesToAdd.Add(new StoreCategory
                    {
                        StoreKey = storeKey,
                        CategoryType = categoryType,
                        OriginalCategoryName = string.Empty
                    });
                }
            }
        }

        if (categoriesToAdd.Count > 0)
        {
            await dbContext.Categories.AddRangeAsync(categoriesToAdd, cancellationToken);
        }

        if (storesToAdd.Count > 0)
        {
            await dbContext.Stores.AddRangeAsync(storesToAdd, cancellationToken);
        }

        if (storeCategoriesToAdd.Count > 0)
        {
            await dbContext.StoreCategories.AddRangeAsync(storeCategoriesToAdd, cancellationToken);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string ExtractWebsiteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : string.Empty;
    }
}
