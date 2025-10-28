using EazyFind.Application;
using EazyFind.Application.Messaging;
using EazyFind.Infrastructure;
using EazyFind.TelegramBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.PostgreSQL(
        builder.Configuration.GetConnectionString("EazyFindDatabase"),
        "public.logs",
        needAutoCreateTable: true)
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.AddSerilog(Log.Logger);

builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName));

builder.Services.AddCoreServices()
                .AddInfrastructureServices(builder.Configuration);

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
