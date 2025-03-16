using EazyFind.Domain.Enums;

namespace EazyFind.Domain.Entities;

public class StoreCategory
{
    public int Id { get; set; }
    public StoreKey StoreKey { get; set; }
    public Store Store { get; set; }
    public string OriginalCategoryName { get; set; }
    public CategoryType CategoryType { get; set; }
    public Category Category { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Product> Products { get; set; }
}
