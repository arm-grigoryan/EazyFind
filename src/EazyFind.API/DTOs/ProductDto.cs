namespace EazyFind.API.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public StoreCategoryDto StoreCategory { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Url { get; set; }
    public string ImageUrl { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
