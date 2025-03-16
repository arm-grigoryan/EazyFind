using EazyFind.Domain.Enums;

namespace EazyFind.Jobs.Configuration;

public class CategoryConfigs
{
    public Dictionary<CategoryType, List<StoreCategoryConfig>> Categories { get; set; }
}

public class StoreCategoryConfig
{
    public StoreKey Store { get; set; }
    public string Url { get; set; }
}