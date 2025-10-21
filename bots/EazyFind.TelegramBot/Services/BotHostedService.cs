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
            "🔍 Փնտրիր ապրանքներ հայկական օնլայն խանութներում\n🔔 Ստացիր անհատական ծանուցումներ քեզ հետաքրքրող ապրանքների համար\n",
            cancellationToken: cancellationToken);

        await _botClient.SetMyDescriptionAsync(
            "🔍 Ակնթարթային որոնում հայկական օնլայն խանութներում։\n" +
            "🔔 Անհատական ծանուցումներ՝ նոր ապրանքների մասին առաջինն իմանալու համար։\n\n" +
            "🤖 Ինչպես օգտագործել բոտը\n\n" +
            "➡️ Սեղմեք <<START>> կոճակը՝ բոտն ակտիվացնելու համար\n" +
            "👉 Հետևեք հրահանգներին՝ որոնում սկսելու կամ ծանուցումներ ստեղծելու համար\n" +
            "💬 Ունեք հարցեր կամ առաջարկներ - գրեք /support բաժնում\n\n" +
            "👇 Գտեք Ձեզ հետաքրքրող ապրանքները վայրկյանների ընթացքում 👇",
            cancellationToken: cancellationToken);
    }

    private Task ConfigureBotCommandsAsync(CancellationToken cancellationToken)
    {
        var commands = new List<BotCommand>
        {
            new() { Command = "info", Description = "Ինչ կարող է անել EazyFind բոտը" },
            new() { Command = "support", Description = "Հարցեր կամ առաջարկներ" },
            new() { Command = "search", Description = "Սկսել նոր որոնում" },
            new() { Command = "myalerts", Description = "Կառավարել ծանուցումները" }
        };

        return _botClient.SetMyCommandsAsync(commands, cancellationToken: cancellationToken);
    }
}
