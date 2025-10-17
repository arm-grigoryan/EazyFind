using EazyFind.Domain.Entities;

namespace EazyFind.Application.Alerts;

public interface IAlertNotificationPublisher
{
    Task PublishAsync(ProductAlert alert, IReadOnlyList<Product> products, int remainingCount, CancellationToken cancellationToken);
}
