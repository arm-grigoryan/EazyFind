namespace EazyFind.TelegramBot.Models;

public enum ConversationStage
{
    None,
    AwaitingSearchText,
    SelectingStores,
    SelectingCategories,
    AwaitingLimit,
    Completed,
    SupportAwaitingMessage
}
