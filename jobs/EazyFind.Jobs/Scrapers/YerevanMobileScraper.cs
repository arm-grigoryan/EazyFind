using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Helpers;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class YerevanMobileScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YerevanMobileScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public YerevanMobileScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<YerevanMobileScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(YerevanMobileScraper));

        var paginationPart = "p";
        var pageNumber = 1;

        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                var htmlString = await httpClient.GetStringAsync(UrlBuilderHelper.AddOrUpdateQueryParam(
                    pageUrl, new Dictionary<string, string> { { paginationPart, pageNumber.ToString() } }), cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var products = htmlDoc.DocumentNode.Descendants("div")
                                                   .Where(x => x.HasClass("product-item-info"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(YerevanMobileScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var itemATagElement = product.Descendants("a")
                                                     .First(x => x.HasClass("product-item-link"));

                        var productUrl = itemATagElement.GetAttributeValue("href", string.Empty);

                        if (internalProducts.Exists(p => p.Url.Equals(productUrl, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            _logger.LogInformation(LogMessages.FinishedScrapingPageItems, nameof(YerevanMobileScraper), pageNumber);
                            _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(YerevanMobileScraper), internalProducts.Count);
                            return internalProducts;
                        }

                        var imageUrl = product.Descendants("img")
                                              .First(x => x.HasClass("product-image-photo"))
                                              .GetAttributeValue("src", string.Empty);

                        var name = itemATagElement.InnerText.Trim();

                        var priceText = product.Descendants("span")
                                               .FirstOrDefault(x => x.GetClasses().Any(x => x == "price"))
                                               ?.InnerText
                                               .Trim();

                        var parsed = false;
                        decimal price = 0;
                        if (priceText is not null)
                        {

                            var cleanPriceText = Regex.Replace(priceText, @"[^\d]", "").Replace(" ", string.Empty);

                            parsed = decimal.TryParse(cleanPriceText, out price);
                        }

                        var internalProduct = new Product
                        {
                            Url = productUrl,
                            ImageUrl = imageUrl,
                            Name = name,
                            Price = parsed ? price : decimal.Zero,
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
                                        nameof(YerevanMobileScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(YerevanMobileScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(YerevanMobileScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(YerevanMobileScraper), internalProducts.Count);
        return internalProducts;
    }
}
