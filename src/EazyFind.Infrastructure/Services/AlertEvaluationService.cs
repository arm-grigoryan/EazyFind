using EazyFind.Application.Alerts;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Infrastructure.Data;
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

        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.StoreCategory)
                .ThenInclude(sc => sc.Store)
            .Include(p => p.StoreCategory)
                .ThenInclude(sc => sc.Category)
            .Where(p => !p.IsDeleted)
            //.Where(p => p.LastSyncedAt >= since)
            .OrderByDescending(p => p.LastSyncedAt)
            .ThenBy(p => p.Id)
            .Where(p => !dbContext.ProductAlertMatches.Any(m => m.AlertId == alert.Id && m.ProductId == p.Id.ToString()));

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

        if (alert.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= alert.MinPrice.Value);
        }

        if (alert.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= alert.MaxPrice.Value);
        }

        var keywords = alert.SearchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        foreach (var keyword in keywords)
        {
            var pattern = $"%{keyword}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
        }

        return await query.Take(limit).ToListAsync(cancellationToken);
    }
}
