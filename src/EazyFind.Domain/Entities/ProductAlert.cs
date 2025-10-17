using System.Collections.Generic;
using System;
namespace EazyFind.Domain.Entities;

public class ProductAlert
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public string SearchText { get; set; }
    public List<string> StoreKeys { get; set; } = new();
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastCheckedUtc { get; set; }

    public ICollection<ProductAlertMatch> Matches { get; set; } = new List<ProductAlertMatch>();
}
