using System;
namespace EazyFind.Domain.Entities;

public class ProductAlertMatch
{
    public long AlertId { get; set; }
    public int ProductId { get; set; }
    public DateTime MatchedAtUtc { get; set; }

    public ProductAlert Alert { get; set; }
    public Product Product { get; set; }
}
