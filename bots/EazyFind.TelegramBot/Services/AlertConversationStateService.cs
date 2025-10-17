using System.Collections.Concurrent;
using EazyFind.TelegramBot.Models;

namespace EazyFind.TelegramBot.Services;

public class AlertConversationStateService
{
    private readonly ConcurrentDictionary<long, AlertCreationSession> _sessions = new();

    public AlertCreationSession GetOrCreate(long chatId)
    {
        return _sessions.GetOrAdd(chatId, _ => new AlertCreationSession());
    }

    public void Clear(long chatId)
    {
        _sessions.TryRemove(chatId, out _);
    }

    public bool TryGet(long chatId, out AlertCreationSession? session)
    {
        return _sessions.TryGetValue(chatId, out session);
    }
}
