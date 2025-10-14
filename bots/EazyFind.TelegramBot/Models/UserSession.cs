using EazyFind.Domain.Enums;

namespace EazyFind.TelegramBot.Models;

public class UserSession
{
    public ConversationStage Stage { get; set; } = ConversationStage.None;
    public string SearchText { get; set; }
    public HashSet<StoreKey> SelectedStores { get; } = [];
    public HashSet<CategoryType> SelectedCategories { get; } = [];
    public int Take { get; set; } = 10;
    public int? StoreSelectionMessageId { get; set; }
    public int? CategorySelectionMessageId { get; set; }

    public bool HasSelectedStores => SelectedStores.Count > 0;
    public bool HasSelectedCategories => SelectedCategories.Count > 0;
}
