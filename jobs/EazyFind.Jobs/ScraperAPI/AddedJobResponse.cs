using System.Text.Json.Serialization;

namespace EazyFind.Jobs.ScraperAPI;

public class AddedJobResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("statusUrl")]
    public string StatusUrl { get; set; }
}
