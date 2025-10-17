using EazyFind.Application;
using EazyFind.Infrastructure;
using EazyFind.Application.Messaging;
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

builder.Services.AddCoreServices()
                .AddInfrastructureServices(builder.Configuration);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.AddSingleton<ConversationStateService>();
builder.Services.AddSingleton<AlertConversationStateService>();
builder.Services.AddSingleton<AlertInteractionService>();
builder.Services.AddSingleton<UpdateHandler>();
builder.Services.AddSingleton<AlertBotService>();
builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddSingleton<ProductSearchService>();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramBotOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});

var host = builder.Build();

await host.RunAsync();
