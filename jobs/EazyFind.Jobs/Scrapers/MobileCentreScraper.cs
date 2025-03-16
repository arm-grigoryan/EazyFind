using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Scrapers.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EazyFind.Jobs.Scrapers;

public class MobileCentreScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JobConfigs _jobConfigs;

    public MobileCentreScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<JobConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(MobileCentreScraper));

        List<Product> internalProducts = [];

        try
        {
            var htmlString = await httpClient.GetStringAsync(pageUrl, cancellationToken);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlString);

            var products = htmlDoc.DocumentNode.Descendants("div")
                                               .Where(x => x.HasClass("listitem"));

            if (products?.Any() is not true)
            {
                Console.WriteLine(string.Format(LogMessages.ProductsNotScraped, nameof(MobileCentreScraper)));
                return internalProducts;
            }

            var index = 0;
            var errorCount = 0;
            foreach (var product in products)
            {
                try
                {
                    var itemATagElement = product.Descendants("a")
                                                 .First(x => x.HasClass("prod-item-img"));

                    var productUrl = itemATagElement.GetAttributeValue("href", string.Empty);

                    var imageNode = itemATagElement.Descendants("img")
                                                   .First();

                    var imageUrl = imageNode.GetAttributeValue("src", string.Empty);

                    if (string.IsNullOrWhiteSpace(imageUrl))
                        imageUrl = imageNode.GetAttributeValue("data-src", string.Empty);

                    var nameAndPriceDiv = product.Descendants("div")
                                                 .First(x => x.HasClass("item-body"));

                    var name = nameAndPriceDiv.ChildNodes
                                              .First(x => x.Name == "h3")
                                              .InnerText
                                              .Trim();

                    var priceText = nameAndPriceDiv.Descendants("span")
                                                   .First(x => x.HasClass("regular"))
                                                   .InnerText
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

                    Console.WriteLine(internalProduct);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine(string.Format(LogMessages.ExceptionOccuredCollectingProductInfo,
                                      nameof(MobileCentreScraper), 0, index, product.InnerHtml, ex));

                    if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                    {
                        throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                            nameof(MobileCentreScraper),
                                            _jobConfigs.MaxErrorCountToContinue), ex);
                    }
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(LogMessages.ExceptionOccuredDuringExecution, nameof(MobileCentreScraper), ex));
            return internalProducts;
        }

        Console.WriteLine(string.Format(LogMessages.TotalScrapedSuccessfully, nameof(MobileCentreScraper), internalProducts.Count));
        return internalProducts;
    }
}
