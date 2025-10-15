using EazyFind.Domain.Entities;

namespace EazyFind.Application.Alerts;

public interface IAlertService
{
    Task<ProductAlert> CreateAsync(AlertCreateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductAlert>> ListAsync(long chatId, CancellationToken cancellationToken);
    Task<ProductAlert?> GetAsync(long chatId, long alertId, CancellationToken cancellationToken);
    Task EnableAsync(long chatId, long alertId, CancellationToken cancellationToken);
    Task DisableAsync(long chatId, long alertId, CancellationToken cancellationToken);
    Task DeleteAsync(long chatId, long alertId, CancellationToken cancellationToken);
    Task PauseAllAsync(long chatId, CancellationToken cancellationToken);
    Task ResumeAllAsync(long chatId, CancellationToken cancellationToken);
}
