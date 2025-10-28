using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Helpers;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class ThreeDPlanetScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ThreeDPlanetScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public ThreeDPlanetScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<ThreeDPlanetScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(ThreeDPlanetScraper));

        var paginationPart = "page";
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
                                                   .Where(x => x.HasClass("product-card"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(ThreeDPlanetScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var productUrl = product.Descendants("a")
                                                .First()
                                                .GetAttributeValue("href", string.Empty);

                        var imageUrl = product.Descendants("img")
                                              .First()
                                              .GetAttributeValue("src", string.Empty);

                        var name = product.Descendants("h3")
                                          .First()
                                          .InnerText;

                        var priceText = product.Descendants("a")
                                               .First()
                                               .InnerText
                                               .Trim();

                        var cleanPriceText = Regex.Replace(priceText, @"[^\d,\.]", string.Empty);
                        cleanPriceText = cleanPriceText.Replace(",", string.Empty)
                                                       .Replace(".", string.Empty);

                        var price = decimal.Parse(cleanPriceText);

                        var internalProduct = new Product
                        {
                            Url = productUrl,
                            ImageUrl = string.Concat("https://3dplanet.am", imageUrl),
                            Name = name,
                            Price = price
                        };

                        internalProducts.Add(internalProduct);
                        index++;
                        errorCount = 0;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, LogMessages.ExceptionOccuredCollectingProductInfo,
                                        nameof(ThreeDPlanetScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(ThreeDPlanetScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(ThreeDPlanetScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(ThreeDPlanetScraper), internalProducts.Count);
        return internalProducts;
    }
}
