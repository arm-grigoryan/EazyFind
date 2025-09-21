using EazyFind.Domain.Enums;

namespace EazyFind.API.DTOs;

public class StoreDto
{
    public StoreKey Key { get; set; }
    public string Name { get; set; }
    public string WebsiteUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
