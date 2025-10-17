using EazyFind.Application;
using EazyFind.Application.Alerts;
using EazyFind.Application.Messaging;
using EazyFind.API.Services;
using EazyFind.Infrastructure;
using EazyFind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// TODOME configure logging

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
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opt => opt.EnableTryItOutByDefault());
}

app.UseHttpsRedirection();

//app.UseSerilogRequestLogging();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
