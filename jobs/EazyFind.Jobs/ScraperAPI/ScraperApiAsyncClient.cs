using EazyFind.Domain.Enums;
using EazyFind.Jobs.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace EazyFind.Jobs.ScraperAPI;

public class ScraperApiAsyncClient
{
    private readonly HttpClient _httpClient;
    private readonly ScraperApiSettings _settings;
    private readonly ScraperSessionManager _sessionManager;
    private readonly ILogger<ScraperApiAsyncClient> _logger;

    public ScraperApiAsyncClient(HttpClient httpClient, IOptions<ScraperApiSettings> settings, ScraperSessionManager sessionManager, ILogger<ScraperApiAsyncClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<string> ScrapeAsync(StoreKey storeKey, string targetUrl, string waitForSelector, CancellationToken cancellationToken)
    {
        var sessionNumber = _sessionManager.GetSessionNumber(storeKey);

        var requestBody = new
        {
            apiKey = _settings.ApiKey,
            urls = new[] { targetUrl },
            apiParams = new Dictionary<string, string>
            {
                ["render"] = "true",
                ["wait_for_selector"] = waitForSelector,
                ["session_number"] = sessionNumber
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://async.scraperapi.com/jobs")
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var jobs = JsonSerializer.Deserialize<List<AddedJobResponse>>(content);

        var statusUrl = jobs?[0]?.StatusUrl;

        if (string.IsNullOrEmpty(statusUrl))
            throw new Exception("No status URL returned from ScraperAPI");

        _logger.LogInformation("Started ScraperAPI job {JobId} for {ScraperKey} (Session: {Session})", jobs[0].Id, storeKey, sessionNumber);

        for (int attempt = 0; attempt < _settings.AsyncJobRetryCount; attempt++)
        {
            await Task.Delay(_settings.AsyncJobRetryInterval, cancellationToken);
            var statusResponse = await _httpClient.GetStringAsync(statusUrl, cancellationToken);
            var jobResult = JsonSerializer.Deserialize<JobStatusResponse>(statusResponse);

            if (jobResult?.Status == "finished")
            {
                if (jobResult?.Response.StatusCode != (int)HttpStatusCode.OK)
                    throw new Exception($"ScraperApi job failed with status code: {jobResult.Response.StatusCode}");

                _logger.LogInformation("ScraperAPI job {JobId} finished successfully for {ScraperKey}", jobs[0].Id, storeKey);
                return jobResult.Response.Body;
            }

            _logger.LogInformation("Waiting for ScraperAPI job {JobId}... (status = {Status})", jobs[0].Id, jobResult?.Status);
        }

        throw new TimeoutException("ScraperAPI job did not finish within the timeout window.");
    }
}
