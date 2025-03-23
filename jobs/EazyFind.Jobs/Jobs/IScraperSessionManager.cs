namespace EazyFind.Jobs.Jobs;

public interface IScraperSessionManager
{
    string GetSessionNumber(string storeKey);
}

internal class ScraperSessionManager : IScraperSessionManager
{
    private readonly Dictionary<string, string> _sessions = [];

    public string GetSessionNumber(string storeKey)
    {
        if (!_sessions.TryGetValue(storeKey, out var session))
        {
            session = new Random().Next(100_000, 999_999).ToString();
            _sessions[storeKey] = session;
        }

        return session;
    }
}
