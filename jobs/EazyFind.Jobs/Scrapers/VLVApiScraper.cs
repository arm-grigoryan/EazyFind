using EazyFind.Domain.Entities;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Constants;
using EazyFind.Jobs.Scrapers.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EazyFind.Jobs.Scrapers;

public class VLVApiScraper : IScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VLVApiScraper> _logger;
    private readonly ScraperConfigs _jobConfigs;

    public VLVApiScraper(
        IHttpClientFactory httpClientFactory,
        ILogger<VLVApiScraper> logger,
        IOptions<ScraperConfigs> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jobConfigs = options.Value;
    }

    public async Task<List<Product>> ScrapeAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(VLVApiScraper));

        const string viewProductUrl = "https://vlv.am/Product/{0}";
        const string imageDomainPart = "https://vlv.am/public/{0}";

        var pageLimitPart = "p";
        var pageLimit = 60;
        var paginationPart = "page";
        var pageNumber = 1;
        var slugPart = "slug";

        List<Product> internalProducts = [];

        while (true)
        {
            try
            {
                HttpRequestMessage BuildHttpRequest()
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, pageUrl);

                    var formData = new MultipartFormDataContent
                    {
                        { new StringContent(pageLimit.ToString()), pageLimitPart },
                        { new StringContent(pageNumber.ToString()), paginationPart },
                        { new StringContent(pageUrl.Split('/')[^1]), slugPart }
                    };
                    request.Content = formData;

                    return request;
                }

                using var request = BuildHttpRequest();
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(LogMessages.ReturnedStatusCode, nameof(VLVApiScraper), response.StatusCode, response.ReasonPhrase);
                    return internalProducts;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var products = JsonSerializer.Deserialize<Root>(jsonContent);

                if (products?.Products?.Count is not > 0)
                {
                    _logger.LogInformation(LogMessages.ProductsNotScraped, nameof(VLVApiScraper));
                    break;
                }

                var index = 0;
                var errorCount = 0;
                foreach (var product in products.Products)
                {
                    try
                    {
                        var productUrl = string.Format(viewProductUrl, product.SellerId);

                        var imageUrl = string.Format(imageDomainPart, product.ThumbnailImageSource);

                        var name = $"{product.Brand.Name} {product.ProductName}";

                        var parsed = decimal.TryParse(product.Pricing.SellingPrice.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);

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
                                        nameof(VLVApiScraper), pageNumber, index, product.SellerId);

                        if (errorCount > _jobConfigs.MaxErrorCountToContinue)
                        {
                            throw new Exception(string.Format(LogMessages.MaxCountOfErrorsReached,
                                                nameof(VLVApiScraper),
                                                _jobConfigs.MaxErrorCountToContinue), ex);
                        }
                        continue;
                    }
                }

                pageNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LogMessages.ExceptionOccuredDuringExecution, nameof(VLVApiScraper));
                break;
            }
        }

        _logger.LogInformation(LogMessages.TotalScrapedSuccessfully, nameof(VLVApiScraper), internalProducts.Count);
        return internalProducts;
    }
}

public class Brand
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Pricing
{
    [JsonPropertyName("selling_price")]
    public string SellingPrice { get; set; }
}

public class VlvProduct
{
    [JsonPropertyName("seller_id")]
    public string SellerId { get; set; }

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; }

    [JsonPropertyName("thumbnail_image_source")]
    public string ThumbnailImageSource { get; set; }

    [JsonPropertyName("brand")]
    public Brand Brand { get; set; }

    [JsonPropertyName("pricing")]
    public Pricing Pricing { get; set; }
}

public class Root
{
    [JsonPropertyName("products")]
    public List<VlvProduct> Products { get; set; }
}
