namespace EazyFind.TelegramBot.Models;

public enum AlertConversationStage
{
    None,
    AwaitingKeywords,
    SelectingStores,
    AwaitingPriceChoice,
    AwaitingMinPrice,
    AwaitingMaxPrice,
    AwaitingConfirmation
}
