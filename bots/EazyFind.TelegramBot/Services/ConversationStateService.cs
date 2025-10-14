using System.Collections.Concurrent;
using EazyFind.TelegramBot.Models;

namespace EazyFind.TelegramBot.Services;

public class ConversationStateService
{
    private readonly ConcurrentDictionary<long, UserSession> _sessions = new();

    public UserSession GetOrCreate(long chatId)
    {
        return _sessions.GetOrAdd(chatId, _ => new UserSession());
    }

    public UserSession Reset(long chatId)
    {
        var session = new UserSession();
        _sessions.AddOrUpdate(chatId, session, (_, _) => session);
        return session;
    }

    public bool TryGet(long chatId, out UserSession? session)
    {
        return _sessions.TryGetValue(chatId, out session);
    }
}
