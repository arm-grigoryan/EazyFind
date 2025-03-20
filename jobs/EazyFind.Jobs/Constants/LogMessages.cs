namespace EazyFind.Jobs.Constants;

public static class LogMessages
{
    public const string ProductsNotScraped = "{Scraper}: No Products found. Finish scraping.";
    public const string ExceptionOccuredDuringExecution = "{Scraper}: Exception occured during execution.";
    public const string ExceptionOccuredCollectingProductInfo =
        "{Scraper}: Exception occured collecting product info: PageNumber: {PageNumber}, ProductNumber: {ProductNumber}, ProductHtml: {ProductHtml}";
    public const string FinishedScrapingPageItems = "{Scraper} finished scraping on pageItems {PageNumber}.";
    public const string TotalScrapedSuccessfully = "{Scraper}: Total {Count} products scraped successfully.";
    public const string ReturnedStatusCode = "{Scraper} returned status code: {StatusCode}. Reason: {ReasonPhrase}.";
    public const string MaxCountOfErrorsReached = "{0}: Maximum count {1} of errors reached. Stop scraping.";
}
