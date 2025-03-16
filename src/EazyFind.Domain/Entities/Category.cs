using EazyFind.Domain.Enums;

namespace EazyFind.Domain.Entities;

public class Category
{
    public CategoryType Type { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<StoreCategory> StoreCategories { get; set; }
}
