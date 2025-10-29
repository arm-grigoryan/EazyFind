using EazyFind.Application.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace EazyFind.API.Controllers;

[ApiController]
[Route(ApiLiterals.Route)]
public class RedirectController(
    IOptions<TelegramBotOptions> botOptions,
    ILogger<RedirectController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult GetRedirectToStore(
        [FromQuery] string url,
        [FromQuery] long chatId,
        [FromQuery] string sig)
    {
        var secret = botOptions.Value.RedirectSecret;
        var expectedBytes = ComputeHmacSha256(secret, url, chatId);

        if (!CryptographicOperations.FixedTimeEquals(
            expectedBytes,
            Convert.FromBase64String(sig)))
        {
            return Forbid();
        }

        logger.LogInformation("BOT_USAGE | Action=Redirect | ChatId={ChatId} | Url={Url}", chatId, url);
        return Redirect(url);
    }

    private static byte[] ComputeHmacSha256(string secret, string url, long chatId)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes($"{chatId}:{url}");

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return hash;
    }
}
