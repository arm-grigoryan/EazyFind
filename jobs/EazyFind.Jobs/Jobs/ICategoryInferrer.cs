using EazyFind.Domain.Enums;

namespace EazyFind.Jobs.Jobs;

public interface ICategoryInferrer
{
    CategoryType? InferCategoryFromName(string name);
}
