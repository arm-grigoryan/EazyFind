using EazyFind.Domain.Entities;

namespace EazyFind.Jobs.Scrapers.Interfaces;

public interface IScraper
{
    Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken);
}
