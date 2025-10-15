namespace EazyFind.Application.Alerts;

public record AlertCreateRequest(
    long ChatId,
    string SearchText,
    IReadOnlyCollection<string> StoreKeys,
    decimal? MinPrice,
    decimal? MaxPrice);
