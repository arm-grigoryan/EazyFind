using System.Web;

namespace EazyFind.Jobs.Helpers;

public static class UrlBuilderHelper
{
    public static string AddOrUpdateQueryParam(string url, Dictionary<string, string> queryParams)
    {
        var uri = new Uri(url);
        var uriBuilder = new UriBuilder(uri);

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var param in queryParams)
        {
            query[param.Key] = param.Value;
        }

        uriBuilder.Query = query.ToString();

        return uriBuilder.ToString();
    }
}
