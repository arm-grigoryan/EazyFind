using EazyFind.Domain.Enums;

namespace EazyFind.API.DTOs;

public class CategoryDto
{
    public CategoryType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}
