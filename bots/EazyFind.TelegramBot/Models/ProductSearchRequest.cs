using EazyFind.Domain.Enums;

namespace EazyFind.TelegramBot.Models;

public class ProductSearchRequest
{
    public int Skip { get; init; }
    public int Take { get; init; }
    public IReadOnlyCollection<StoreKey> Stores { get; init; } = [];
    public IReadOnlyCollection<CategoryType> Categories { get; init; } = [];
    public string SearchText { get; init; }
    public long ChatId { get; init; }
}
