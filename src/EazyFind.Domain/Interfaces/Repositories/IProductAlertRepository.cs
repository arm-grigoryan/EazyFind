using EazyFind.Domain.Entities;

namespace EazyFind.Domain.Interfaces.Repositories;

public interface IProductAlertRepository
{
    Task<ProductAlert> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductAlert>> GetByChatIdAsync(long chatId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductAlert>> GetActiveAsync(CancellationToken cancellationToken);
    Task<ProductAlert> CreateAsync(ProductAlert alert, CancellationToken cancellationToken);
    Task UpdateAsync(ProductAlert alert, CancellationToken cancellationToken);
    Task DeleteAsync(long id, CancellationToken cancellationToken);
    Task SetActiveAsync(long alertId, bool isActive, CancellationToken cancellationToken);
    Task SetActiveForChatAsync(long chatId, bool isActive, CancellationToken cancellationToken);
    Task UpdateLastCheckedAsync(long alertId, DateTime lastCheckedUtc, CancellationToken cancellationToken);
}
