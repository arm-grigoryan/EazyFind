using EazyFind.Application;
using EazyFind.Infrastructure;
using EazyFind.TelegramBot.Options;
using EazyFind.TelegramBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName));

builder.Services.AddCoreServices();

var connectionString = builder.Configuration.GetConnectionString("EazyFindDatabase");
if (string.IsNullOrWhiteSpace(connectionString) ||
    connectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Connection string 'EazyFindDatabase' is not configured. Please update appsettings.json.");
}

builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.AddSingleton<ConversationStateService>();
builder.Services.AddSingleton<UpdateHandler>();
builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddSingleton<ProductSearchService>();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramBotOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.BotToken) || options.BotToken == TelegramBotOptions.PlaceholderToken)
    {
        throw new InvalidOperationException("Telegram bot token is not configured. Please update appsettings.json.");
    }

    return new TelegramBotClient(options.BotToken);
});

var host = builder.Build();

await host.RunAsync();
