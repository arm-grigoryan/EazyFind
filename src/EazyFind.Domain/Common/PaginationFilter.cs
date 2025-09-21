using System.ComponentModel.DataAnnotations;

namespace EazyFind.Domain.Common;

public class PaginationFilter
{
    [Range(0, int.MaxValue)]
    public int Skip { get; set; } = 0;

    [Range(1, 100)]
    public int Take { get; set; } = 5;

    // Parameterless constructor required for model binding
    public PaginationFilter() { }

    // Optional: convenience constructor
    public PaginationFilter(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }
}
