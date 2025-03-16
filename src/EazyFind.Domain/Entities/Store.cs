using EazyFind.Domain.Enums;

namespace EazyFind.Domain.Entities;

public class Store
{
    public StoreKey Key { get; set; }
    public string Name { get; set; }
    public string WebsiteUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<StoreCategory> StoreCategories { get; set; }
}
