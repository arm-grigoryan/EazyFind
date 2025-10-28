using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Helpers;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class ZigzagScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZigzagScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public ZigzagScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<ZigzagScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(ZigzagScraper));

        var paginationPart = "p";
        var pageNumber = 1;

        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                var urlToCall = UrlBuilderHelper.AddOrUpdateQueryParam(
                    pageUrl, new Dictionary<string, string> { { paginationPart, pageNumber.ToString() } });

                var fullUrl = $"https://api.zenrows.com/v1/?apikey=ce56176bd3197e47a78881d1f6fe5c2e1a89e231&url={urlToCall}";
                //var response = await httpClient.GetAsync(fullUrl, cancellationToken);

                //if (!response.IsSuccessStatusCode)
                //{
                //    _logger.LogError("Failed call with error: {ErrorContent}, Reason Phrase: {Reason}", await response.Content.ReadAsStringAsync(cancellationToken), response.ReasonPhrase);
                //    foreach (var header in response.Headers)
                //        _logger.LogError("Failed call response header key-value: {HeaderKey}-{HeaderValue}", header.Key, header.Value);
                //}

                var htmlString = await httpClient.GetStringAsync(fullUrl, cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var products = htmlDoc.DocumentNode.Descendants("div")
                                                   .Where(x => x.HasClass("product_block"));

                if (products?.Any() is not true)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(ZigzagScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products)
                {
                    try
                    {
                        var itemATagElement = product.Descendants("a")
                                                     .First(x => x.HasClass("product_name"));

                        var productUrl = itemATagElement.GetAttributeValue("href", string.Empty);

                        if (internalProducts.Exists(p => p.Url.Equals(productUrl, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            _logger.LogInformation(LogMessages.FinishedScrapingPageItems, nameof(ZigzagScraper), pageNumber);
                            _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(ZigzagScraper), internalProducts.Count);
                            return internalProducts;
                        }

                        var imageUrl = product.Descendants("img")
                                              .First()
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
                                        nameof(ZigzagScraper), pageNumber, index, product.InnerHtml);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(ZigzagScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(ZigzagScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(ZigzagScraper), internalProducts.Count);
        return internalProducts;
    }
}
