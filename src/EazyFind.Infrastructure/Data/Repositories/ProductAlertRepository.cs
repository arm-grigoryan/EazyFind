using System.Collections.Generic;
using System.Linq;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Data.Repositories;

internal class ProductAlertRepository(EazyFindDbContext dbContext) : IProductAlertRepository
{
    public Task<ProductAlert> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return dbContext.ProductAlerts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductAlert>> GetByChatIdAsync(long chatId, CancellationToken cancellationToken)
    {
        return await dbContext.ProductAlerts
            .AsNoTracking()
            .Where(a => a.ChatId == chatId)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductAlert>> GetActiveAsync(CancellationToken cancellationToken)
    {
        return await dbContext.ProductAlerts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductAlert> CreateAsync(ProductAlert alert, CancellationToken cancellationToken)
    {
        await dbContext.ProductAlerts.AddAsync(alert, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public Task UpdateAsync(ProductAlert alert, CancellationToken cancellationToken)
    {
        dbContext.ProductAlerts.Update(alert);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        return dbContext.ProductAlerts
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SetActiveAsync(long alertId, bool isActive, CancellationToken cancellationToken)
    {
        return dbContext.ProductAlerts
            .Where(a => a.Id == alertId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsActive, isActive), cancellationToken);
    }

    public Task SetActiveForChatAsync(long chatId, bool isActive, CancellationToken cancellationToken)
    {
        return dbContext.ProductAlerts
            .Where(a => a.ChatId == chatId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsActive, isActive), cancellationToken);
    }

    public Task UpdateLastCheckedAsync(long alertId, DateTime lastCheckedUtc, CancellationToken cancellationToken)
    {
        return dbContext.ProductAlerts
            .Where(a => a.Id == alertId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.LastCheckedUtc, lastCheckedUtc), cancellationToken);
    }
}
