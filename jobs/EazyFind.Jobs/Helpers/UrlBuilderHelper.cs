using System.Web;

namespace EazyFind.Jobs.Helpers;

public static class UrlBuilderHelper
{
    public static string AddOrUpdateQueryParam(string url, string key, string value)
    {
        var uri = new Uri(url);
        var uriBuilder = new UriBuilder(uri);

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query[key] = value;

        uriBuilder.Query = query.ToString();

        return uriBuilder.ToString();
    }
}
