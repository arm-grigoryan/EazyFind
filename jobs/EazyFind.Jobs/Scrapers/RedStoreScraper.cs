using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;

namespace EazyFind.Jobs.Scrapers;

public class RedStoreScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RedStoreScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public RedStoreScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<RedStoreScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(RedStoreScraper));

        var baseUrl = "https://redstore.am{0}";

        var paginationPart = "per_page=";
        var pageNumber = 0;
        var pageChange = 12;

        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                var htmlString = await httpClient.GetStringAsync($"{pageUrl}?{paginationPart}{pageNumber}", cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var products = htmlDoc.DocumentNode.Descendants("li")
                                                   .Where(x => x.HasClass("globalFrameProduct"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(RedStoreScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var productUrl = product.Descendants("a")
                                                .First(x => x.HasClass("frame-photo-title"))
                                                .GetAttributeValue("href", string.Empty);

                        var imageUrl = product.Descendants("img")
                                              .First(x => x.HasClass("lazy"))
                                              .GetAttributeValue("src", string.Empty);
                        var fullImageUrl = string.Format(baseUrl, imageUrl);

                        var name = product.Descendants("span")
                                          .First(x => x.HasClass("title"))
                                          .InnerText;

                        var priceText = product.Descendants("span")
                                               .First(x => x.HasClass("priceCashVariant"))
                                               .InnerText
                                               .Trim();

                        var normalizedPriceText = priceText.Replace(",", string.Empty)
                                                           .Replace(".", string.Empty);

                        var price = decimal.Parse(normalizedPriceText);

                        var internalProduct = new Product
                        {
                            Url = productUrl,
                            ImageUrl = fullImageUrl,
                            Name = name,
                            Price = price
                        };

                        internalProducts.Add(internalProduct);
                        index++;
                        errorCount = 0;

                        //Console.WriteLine(internalProduct);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, LogMessages.ExceptionOccuredCollectingProductInfo,
                                        nameof(RedStoreScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(RedStoreScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber += pageChange;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(RedStoreScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(RedStoreScraper), internalProducts.Count);
        return internalProducts;
    }
}
