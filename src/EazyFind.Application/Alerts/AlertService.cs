using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Interfaces.Repositories;

namespace EazyFind.Application.Alerts;

internal class AlertService(
    IProductAlertRepository alertRepository) : IAlertService
{
    public async Task<ProductAlert> CreateAsync(AlertCreateRequest request, CancellationToken cancellationToken)
    {
        var searchText = (request.SearchText ?? string.Empty).Trim();
        if (searchText.Length < 2)
        {
            throw new ArgumentException("Search text must be at least 2 characters long.", nameof(request));
        }

        var minPrice = request.MinPrice;
        var maxPrice = request.MaxPrice;
        if (minPrice is < 0)
        {
            throw new ArgumentException("Minimum price must be non-negative.", nameof(request));
        }

        if (maxPrice is < 0)
        {
            throw new ArgumentException("Maximum price must be non-negative.", nameof(request));
        }

        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
        {
            throw new ArgumentException("Minimum price cannot be greater than maximum price.", nameof(request));
        }

        var storeKeys = NormalizeStoreKeys(request.StoreKeys);

        var alert = new ProductAlert
        {
            ChatId = request.ChatId,
            SearchText = searchText,
            StoreKeys = storeKeys,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return await alertRepository.CreateAsync(alert, cancellationToken);
    }

    public Task<IReadOnlyList<ProductAlert>> ListAsync(long chatId, CancellationToken cancellationToken)
    {
        return alertRepository.GetByChatIdAsync(chatId, cancellationToken);
    }

    public async Task<ProductAlert> GetAsync(long chatId, long alertId, CancellationToken cancellationToken)
    {
        var alert = await alertRepository.GetByIdAsync(alertId, cancellationToken);
        return alert is null || alert.ChatId != chatId ? null : alert;
    }

    public async Task EnableAsync(long chatId, long alertId, CancellationToken cancellationToken)
    {
        var alert = await GetOwnedAlertAsync(chatId, alertId, cancellationToken);
        if (!alert.IsActive)
        {
            await alertRepository.SetActiveAsync(alert.Id, true, cancellationToken);
        }
    }

    public async Task DisableAsync(long chatId, long alertId, CancellationToken cancellationToken)
    {
        var alert = await GetOwnedAlertAsync(chatId, alertId, cancellationToken);
        if (alert.IsActive)
        {
            await alertRepository.SetActiveAsync(alert.Id, false, cancellationToken);
        }
    }

    public async Task DeleteAsync(long chatId, long alertId, CancellationToken cancellationToken)
    {
        await GetOwnedAlertAsync(chatId, alertId, cancellationToken);
        await alertRepository.DeleteAsync(alertId, cancellationToken);
    }

    public Task PauseAllAsync(long chatId, CancellationToken cancellationToken)
    {
        return alertRepository.SetActiveForChatAsync(chatId, false, cancellationToken);
    }

    public Task ResumeAllAsync(long chatId, CancellationToken cancellationToken)
    {
        return alertRepository.SetActiveForChatAsync(chatId, true, cancellationToken);
    }

    private async Task<ProductAlert> GetOwnedAlertAsync(long chatId, long alertId, CancellationToken cancellationToken)
    {
        var alert = await alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert is null || alert.ChatId != chatId)
        {
            throw new InvalidOperationException("Alert not found.");
        }

        return alert;
    }

    private static List<string> NormalizeStoreKeys(IReadOnlyCollection<string> storeKeys)
    {
        if (storeKeys is null || storeKeys.Count == 0)
        {
            return new List<string>();
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in storeKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!Enum.TryParse<StoreKey>(key, true, out var parsed))
            {
                throw new ArgumentException($"Invalid store key '{key}'.", nameof(storeKeys));
            }

            normalized.Add(parsed.ToString());
        }

        return normalized.Select(s => Enum.Parse<StoreKey>(s).ToString()).ToList();
    }
}
