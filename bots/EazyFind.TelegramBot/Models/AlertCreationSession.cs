using EazyFind.Domain.Enums;

namespace EazyFind.TelegramBot.Models;

public class AlertCreationSession
{
    public AlertConversationStage Stage { get; set; } = AlertConversationStage.None;
    public string Keywords { get; set; } = string.Empty;
    public HashSet<StoreKey> SelectedStores { get; } = [];
    public bool IncludeAllStores { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? StoreSelectionMessageId { get; set; }
}
