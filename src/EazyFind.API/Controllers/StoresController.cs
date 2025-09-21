using Microsoft.AspNetCore.Mvc;

namespace EazyFind.API.Controllers;

[ApiController]
[Route(ApiLiterals.Route)]
public class StoresController : ControllerBase
{
    /// <summary>
    /// Get all stores
    /// </summary>
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // TODOME finish
    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAllStores(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
