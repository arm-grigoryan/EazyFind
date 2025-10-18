using EazyFind.Application.Alerts;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Infrastructure.Data;
using EazyFind.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Services;

internal class AlertEvaluationService(EazyFindDbContext dbContext) : IAlertEvaluationService
{
    public async Task<IReadOnlyList<Product>> GetCandidatesAsync(ProductAlert alert, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            limit = 50;
        }

        var since = alert.LastCheckedUtc ?? DateTime.UtcNow.AddHours(-24);

        var matchedIdsSet = (await dbContext.ProductAlertMatches
            .Where(m => m.AlertId == alert.Id)
            .Select(m => m.ProductId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.StoreCategory).ThenInclude(sc => sc.Store)
            .Include(p => p.StoreCategory).ThenInclude(sc => sc.Category)
            .Where(p => !p.IsDeleted && p.LastSyncedAt >= since && !matchedIdsSet.Contains(p.Id))
            .WhereIf(alert.MinPrice.HasValue, p => p.Price >= alert.MinPrice.Value)
            .WhereIf(alert.MaxPrice.HasValue, p => p.Price <= alert.MaxPrice.Value);

        if (alert.StoreKeys is { Count: > 0 })
        {
            var storeEnums = alert.StoreKeys
                .Select(key => Enum.TryParse<StoreKey>(key, true, out var parsed) ? parsed : (StoreKey?)null)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToArray();

            if (storeEnums.Length > 0)
            {
                query = query.Where(p => storeEnums.Contains(p.StoreCategory.StoreKey));
            }
        }

        if (!string.IsNullOrWhiteSpace(alert.SearchText))
        {
            var keywords = alert.SearchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

            foreach (var keyword in keywords)
            {
                var pattern = $"%{keyword}%";
                query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
            }
        }

        query = query
            .OrderByDescending(p => p.LastSyncedAt)
            .ThenBy(p => p.Id);

        return await query.Take(limit).ToListAsync(cancellationToken);
    }
}
