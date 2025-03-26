using System.Text.Json.Serialization;

namespace EazyFind.Jobs.ScraperAPI;

public class JobStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("response")]
    public Response Response { get; set; }
}

public class Response
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; }
}
