namespace EazyFind.Jobs.Configuration;

public class ScraperApiSettings
{
    public int AsyncJobRetryCount { get; set; }
    public int AsyncJobRetryInterval { get; set; }
    public string ApiKey { get; set; }
}
