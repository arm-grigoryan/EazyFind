using AutoMapper;
using EazyFind.API.DTOs;
using EazyFind.Application.Products;
using EazyFind.Domain.Common;
using EazyFind.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EazyFind.API.Controllers;

[ApiController]
[Route(ApiLiterals.Route)]
public class ProductsController(IProductService service, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Get products
    /// </summary>
    [ProducesResponseType(typeof(PaginatedResult<ProductDto>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ProductDto>>> GetPaginatedProducts(
        [FromQuery] PaginationFilter paginationFilter,
        [FromQuery] StoreKey? store,
        [FromQuery] CategoryType? category,
        [FromQuery, MinLength(3)] string searchText,
        CancellationToken cancellationToken = default)
    {
        var products = await service.GetPaginatedAsync(paginationFilter, store, category, searchText, cancellationToken);

        var result = new PaginatedResult<ProductDto>
        {
            TotalCount = products.TotalCount,
            Items = mapper.Map<List<ProductDto>>(products.Items)
        };

        return Ok(result);
    }
}
