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
    private readonly JobConfigs _jobConfigs;

    public RedStoreScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<JobConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
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
                    Console.WriteLine(string.Format(LogMessages.ProductsNotScraped, nameof(RedStoreScraper)));
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

                        Console.WriteLine(internalProduct);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine(string.Format(LogMessages.ExceptionOccuredCollectingProductInfo,
                                          nameof(RedStoreScraper), 0, index, product.InnerHtml, ex));

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
                Console.WriteLine(string.Format(LogMessages.ExceptionOccuredDuringExecution, nameof(RedStoreScraper), ex));
                break;
            }
        }

        Console.WriteLine(string.Format(LogMessages.TotalScrapedSuccessfully, nameof(RedStoreScraper), internalProducts.Count));
        return internalProducts;
    }
}
