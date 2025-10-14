using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;

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

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = []
        };

        _botClient.StartReceiving(_updateHandler, receiverOptions, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
