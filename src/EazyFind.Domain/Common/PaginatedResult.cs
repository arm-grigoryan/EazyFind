namespace EazyFind.Domain.Common;

public class PaginatedResult<T>
{
    public int TotalCount { get; set; }
    public required ICollection<T> Items { get; set; }
}
