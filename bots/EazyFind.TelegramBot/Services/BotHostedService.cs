using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace EazyFind.TelegramBot.Services;

public class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateHandler _updateHandler;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(ITelegramBotClient botClient, UpdateHandler updateHandler, ILogger<BotHostedService> logger)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _botClient.GetMeAsync(cancellationToken: stoppingToken);
        _logger.LogInformation("Telegram bot connected as {Username}", me.Username);

        await ConfigureBotProfileAsync(stoppingToken);
        await ConfigureBotCommandsAsync(stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = []
        };

        _botClient.StartReceiving(_updateHandler, receiverOptions, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ConfigureBotProfileAsync(CancellationToken cancellationToken)
    {
        await _botClient.SetMyShortDescriptionAsync(
            "Search products and manage deal alerts in seconds.",
            cancellationToken: cancellationToken);

        await _botClient.SetMyDescriptionAsync(
            "EazyFind helps you explore stores in real time and track new deals. " +
            "Use /search to look up products, /myalerts to manage notifications, and /support to reach the team.",
            cancellationToken: cancellationToken);
    }

    private Task ConfigureBotCommandsAsync(CancellationToken cancellationToken)
    {
        var commands = new List<BotCommand>
        {
            new() { Command = "info", Description = "What EazyFind can do" },
            new() { Command = "support", Description = "Get help or share feedback" },
            new() { Command = "feedback", Description = "Share suggestions with the team" },
            new() { Command = "search", Description = "Search for products" },
            new() { Command = "myalerts", Description = "Manage your alerts" }
        };

        return _botClient.SetMyCommandsAsync(commands, cancellationToken: cancellationToken);
    }
}
