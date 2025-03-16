namespace EazyFind.Jobs.Constants;

public static class LogMessages
{
    public const string ProductsNotScraped = "{0}: No Products found. Finish scraping.";
    public const string ExceptionOccuredDuringExecution = "{0}: Exception occured during execution: {1}";
    public const string ExceptionOccuredCollectingProductInfo =
        "{0}: Exception occured collecting product info: PageNumber: {1}, ProductNumber: {2}, ProductHtml: {3}, ex: {4}";
    public const string TotalScrapedSuccessfully = "{0}: Total {1} products scraped successfully.";
    public const string MaxCountOfErrorsReached = "{0}: Maximum count {1} of errors reached. Stop scraping.";
}
