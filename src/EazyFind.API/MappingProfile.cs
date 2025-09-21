using AutoMapper;
using EazyFind.API.DTOs;
using EazyFind.Domain.Entities;

namespace EazyFind.API;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Store, StoreDto>();
        CreateMap<Category, CategoryDto>();
        CreateMap<StoreCategory, StoreCategoryDto>();
        CreateMap<Product, ProductDto>();
    }
}
