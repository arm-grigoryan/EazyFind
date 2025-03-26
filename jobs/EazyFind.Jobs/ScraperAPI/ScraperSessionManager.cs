using EazyFind.Domain.Enums;

namespace EazyFind.Jobs.ScraperAPI;

public class ScraperSessionManager
{
    private readonly Dictionary<StoreKey, string> _sessions = [];

    public string GetSessionNumber(StoreKey storeKey)
    {
        if (!_sessions.TryGetValue(storeKey, out var session))
        {
            session = new Random().Next(100_000, 999_999).ToString();
            _sessions[storeKey] = session;
        }

        return session;
    }
}
