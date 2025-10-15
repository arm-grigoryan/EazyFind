using EazyFind.Domain.Entities;

namespace EazyFind.Domain.Interfaces.Repositories;

public interface IProductAlertMatchRepository
{
    Task BulkInsertAsync(IEnumerable<ProductAlertMatch> matches, CancellationToken cancellationToken);
}
