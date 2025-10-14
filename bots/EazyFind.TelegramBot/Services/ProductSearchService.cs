using EazyFind.Application.Products;
using EazyFind.Domain.Common;
using EazyFind.Domain.Entities;
using EazyFind.TelegramBot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EazyFind.TelegramBot.Services;

public class ProductSearchService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductSearchService> _logger;

    public ProductSearchService(IServiceScopeFactory scopeFactory, ILogger<ProductSearchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<PaginatedResult<Product>> SearchAsync(ProductSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

            var pagination = new PaginationFilter(request.Skip, request.Take);
            var stores = request.Stores?.ToList() ?? [];
            var categories = request.Categories?.ToList() ?? [];
            var searchText = request.SearchText?.Trim() ?? string.Empty;

            _logger.LogInformation(
                "TelegramBotSearch ChatId={ChatId} Stores={Stores} Categories={Categories} Take={Take} SearchText={SearchText}",
                request.ChatId,
                string.Join(',', stores),
                string.Join(',', categories),
                request.Take,
                searchText);

            return await productService.GetPaginatedAsync(pagination, stores, categories, searchText, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve products for ChatId={ChatId}", request.ChatId);
            return null;
        }
    }
}
