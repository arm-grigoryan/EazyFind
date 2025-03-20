using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class VDComputersScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VDComputersScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public VDComputersScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<VDComputersScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(VDComputersScraper));

        var pageLimitPart = "per_page=";
        var pageLimit = 36;
        var paginationPart = "page/";
        var pageNumber = 1;

        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                var htmlString = await httpClient.GetStringAsync($"{pageUrl}/{paginationPart}{pageNumber}/?{pageLimitPart}{pageLimit}", cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var products = htmlDoc.DocumentNode.Descendants("div")
                                                   .Where(x => x.HasClass("product-grid-item"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(VDComputersScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var itemATagElement = product.Descendants("h3")
                                                     .First(x => x.GetAttributeValue("class", string.Empty) == "wd-entities-title")
                                                     .ChildNodes
                                                     .First(node => node.Name == "a");

                        var productUrl = itemATagElement.GetAttributeValue("href", string.Empty);

                        var imageUrl = product.Descendants("img")
                                              .First()
                                              .GetAttributeValue("src", string.Empty);

                        var name = itemATagElement.InnerText.Trim();

                        var priceText = product.Descendants("span")
                                               .LastOrDefault(x => x.HasClass("screen-reader-text"))
                                               ?.InnerText
                                               .Trim();

                        var parsed = false;
                        decimal price = 0;
                        if (priceText is not null)
                        {

                            var cleanPriceText = Regex.Replace(priceText, @"[^\d]", "").Replace(" ", string.Empty);

                            cleanPriceText = cleanPriceText.Replace(",", string.Empty)
                                                           .Replace(".", string.Empty);

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
                                        nameof(VDComputersScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(VDComputersScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(VDComputersScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(VDComputersScraper), internalProducts.Count);
        return internalProducts;
    }
}
