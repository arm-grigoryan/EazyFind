using EazyFind.Domain.Entities;

namespace EazyFind.Application.Alerts;

public interface IAlertEvaluationService
{
    Task<IReadOnlyList<Product>> GetCandidatesAsync(ProductAlert alert, int limit, CancellationToken cancellationToken);
}
