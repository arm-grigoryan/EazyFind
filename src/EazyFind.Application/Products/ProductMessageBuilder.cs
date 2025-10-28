using System.Net;
using System.Text;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Extensions;
using EazyFind.Domain.Enums;

namespace EazyFind.Application.Products;

internal class ProductMessageBuilder : IProductMessageBuilder
{
    public ProductMessage Build(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var captionBuilder = new StringBuilder();
        captionBuilder.AppendLine($"<b>{Escape(product.Name)}</b>");
        captionBuilder.AppendLine($"Գին: {product.Price:0.##}");

        var storeName = product.StoreCategory?.Store?.Name;
        if (!string.IsNullOrWhiteSpace(storeName))
        {
            captionBuilder.AppendLine($"Խանութ: {Escape(storeName)}");
        }
        else if (product.StoreCategory is { StoreKey: var storeKey })
        {
            captionBuilder.AppendLine($"Խանութ: {Escape(storeKey.ToDisplayName())}");
        }

        string categoryLabel = null;
        if (product.StoreCategory?.Category?.Type is CategoryType resolvedCategory)
        {
            categoryLabel = resolvedCategory.ToDisplayName();
        }
        else if (product.StoreCategory is { CategoryType: var categoryType })
        {
            categoryLabel = categoryType.ToDisplayName();
        }

        if (string.IsNullOrWhiteSpace(categoryLabel) &&
            !string.IsNullOrWhiteSpace(product.StoreCategory?.OriginalCategoryName))
        {
            categoryLabel = product.StoreCategory!.OriginalCategoryName;
        }

        if (!string.IsNullOrWhiteSpace(categoryLabel))
        {
            captionBuilder.AppendLine($"Կատեգորիա: {Escape(categoryLabel)}");
        }

        captionBuilder.AppendLine($"Թարմացվել է: {product.LastSyncedAt.AddHours(4):G}");

        var caption = captionBuilder.ToString().Trim();
        var photo = string.IsNullOrWhiteSpace(product.ImageUrl) ? null : product.ImageUrl;
        var url = string.IsNullOrWhiteSpace(product.Url) ? null : product.Url;

        return new ProductMessage(photo, caption, url);
    }

    private static string Escape(string value) => string.IsNullOrEmpty(value) ? string.Empty : WebUtility.HtmlEncode(value);
}
