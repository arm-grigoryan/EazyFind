using EazyFind.API.Services;
using EazyFind.Application;
using EazyFind.Application.Alerts;
using EazyFind.Application.Messaging;
using EazyFind.Infrastructure;
using EazyFind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json.Serialization;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, configuration) =>
{
    var connectionString = context.Configuration.GetConnectionString("EazyFindDatabase");

    configuration.WriteTo.PostgreSQL(connectionString, "public.logs", needAutoCreateTable: true)
        .MinimumLevel.Information();

    if (!context.HostingEnvironment.IsProduction())
    {
        configuration.WriteTo.Console()
        .MinimumLevel.Information();
    }
});

services.AddControllers()
        .AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.AddAutoMapper(typeof(Program));

services.AddCoreServices()
        .AddInfrastructureServices(configuration);

services.Configure<AlertOptions>(configuration.GetSection(AlertOptions.SectionName));
services.Configure<TelegramBotOptions>(configuration.GetSection(TelegramBotOptions.SectionName));

services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});

services.AddSingleton<IAlertNotificationPublisher, TelegramAlertNotificationPublisher>();
services.AddHostedService<AlertEvaluatorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EazyFindDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opt => opt.EnableTryItOutByDefault());
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
