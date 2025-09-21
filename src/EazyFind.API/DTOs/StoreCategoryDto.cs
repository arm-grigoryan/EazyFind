namespace EazyFind.API.DTOs;

public class StoreCategoryDto
{
    public int Id { get; set; }
    public StoreDto Store { get; set; }
    public string OriginalCategoryName { get; set; }
    public CategoryDto Category { get; set; }
    public DateTime CreatedAt { get; set; }
}
