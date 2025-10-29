using Microsoft.AspNetCore.Mvc;

namespace EazyFind.API.Controllers;

[ApiController]
[Route(ApiLiterals.Route)]
public class RedirectController(ILogger<RedirectController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult GetRedirectToStore(
        [FromQuery] string url,
        [FromQuery] long chatId)
    {
        logger.LogInformation("BOT_USAGE | Action=Redirect | ChatId={ChatId} | Url={Url}", chatId, url);
        return Redirect(url);
    }
}
