using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class AllCellScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AllCellScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public AllCellScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<AllCellScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(AllCellScraper));

        var paginationPart = "/page/{0}/";
        var pageNumber = 1;
        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                var htmlString = await httpClient.GetStringAsync($"{pageUrl}{string.Format(paginationPart, pageNumber)}", cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var products = htmlDoc.DocumentNode.Descendants("div")
                                                   .Where(x => x.HasClass("product-inner"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(AllCellScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var productUrl = product.Descendants("a")
                                                .First(x => x.GetAttributeValue("class", string.Empty).Contains("product__link"))
                                                .GetAttributeValue("href", string.Empty);

                        var imageUrl = product.Descendants("img").First()
                                              .GetAttributeValue("data-src", string.Empty);

                        var name = product.Descendants("h2")
                                          .First(x => x.GetAttributeValue("class", string.Empty).Contains("product__title"))
                                          .InnerText;

                        var priceText = product.Descendants("bdi")
                                               .FirstOrDefault()
                                               ?.InnerText
                                               ?.Trim();

                        var parsed = false;
                        decimal price = 0;
                        if (priceText is not null)
                        {
                            var cleanPriceText = Regex.Replace(priceText, @"[^\d,\.]", string.Empty);
                            cleanPriceText = cleanPriceText.Replace(",", string.Empty)
                                                           .Replace(".", string.Empty);

                            parsed = decimal.TryParse(cleanPriceText, out price);
                        }

                        var internalProduct = new Product
                        {
                            Url = productUrl,
                            ImageUrl = imageUrl,
                            Name = name,
                            Price = parsed ? price : decimal.Zero
                        };

                        internalProducts.Add(internalProduct);
                        index++;
                        errorCount = 0;

                        Console.WriteLine(internalProduct);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, LogMessages.ExceptionOccuredCollectingProductInfo,
                                        nameof(AllCellScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(AllCellScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(AllCellScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(AllCellScraper), internalProducts.Count);
        return internalProducts;
    }
}
