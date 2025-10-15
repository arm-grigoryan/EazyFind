using EazyFind.Domain.Entities;

namespace EazyFind.Application.Products;

public interface IProductMessageBuilder
{
    ProductMessage Build(Product product);
}
