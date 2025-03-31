using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class VenusScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VenusScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public VenusScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<VenusScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(VenusScraper));

        var pageLimitPart = "limit=";
        var pageLimit = 100;
        var paginationPart = "page=";
        var pageNumber = 1;
        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                var htmlString = await httpClient.GetStringAsync($"{pageUrl}?{paginationPart}{pageNumber}&{pageLimitPart}{pageLimit}");

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var products = htmlDoc.DocumentNode.Descendants("div")
                                                   .Where(x => x.HasClass("product-block"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(VenusScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var productUrl = product.Descendants("a").First()
                                                .GetAttributeValue("href", string.Empty);

                        var imageUrl = product.Descendants("img")
                                              .First(x => x.HasClass("img-responsive"))
                                              .GetAttributeValue("src", string.Empty);

                        var name = product.Descendants("h4").First()
                                          .Descendants("a").First()
                                          .InnerText;

                        var priceText = product.Descendants("p")
                                               .First(x => x.HasClass("price"))
                                               .InnerText
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

                        Console.WriteLine(internalProduct);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, LogMessages.ExceptionOccuredCollectingProductInfo,
                                        nameof(VenusScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(VenusScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(VenusScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(VenusScraper), internalProducts.Count);
        return internalProducts;
    }
}
