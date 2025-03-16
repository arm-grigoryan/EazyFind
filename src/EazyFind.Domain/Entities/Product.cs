namespace EazyFind.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public int StoreCategoryId { get; set; }
    public StoreCategory StoreCategory { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Url { get; set; }
    public string ImageUrl { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletionDate { get; set; }

    public override string ToString()
    {
        return $"{nameof(Url)}: {Url}\n" +
               $"{nameof(ImageUrl)}: {ImageUrl}\n" +
               $"{nameof(Name)}: {Name}\n" +
               $"{nameof(Price)}: {Price}\n";
    }
}
