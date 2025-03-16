using EazyFind.Application;
using EazyFind.Domain.Enums;
using EazyFind.Infrastructure;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Extensions;
using EazyFind.Jobs.Jobs;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.Configure<CategoryConfigs>(configuration.GetSection(nameof(CategoryConfigs)));
services.Configure<JobConfigs>(configuration.GetSection(nameof(JobConfigs)));

services.AddCoreServices()
        .AddInfrastructureServices(configuration)
        .AddHttpClients()
        .AddScrapers()
        .AddJobs();

services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(configuration.GetConnectionString("HangfireDatabase")));
});
builder.Services.AddHangfireServer(options => options.WorkerCount = 3);

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var jobService = scope.ServiceProvider.GetRequiredService<ScraperJob>();
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    recurringJobs.AddOrUpdate(
        "Laptops-Scraper-Job",
        () => jobService.RunScrapersAsync(CategoryType.Laptops),
        "0 9 * * *",  // Runs daily at 9:00 AM UTC
        new RecurringJobOptions()
    );

    recurringJobs.AddOrUpdate(
        "Smartphones-Scraper-Job",
        () => jobService.RunScrapersAsync(CategoryType.Smartphones),
        "30 9 * * *", // Runs daily at 9:30 AM UTC
        new RecurringJobOptions()
    );

    recurringJobs.AddOrUpdate(
        "Monitors-Scraper-Job",
        () => jobService.RunScrapersAsync(CategoryType.Monitors),
        "0 10 * * *", // Runs daily at 10:00 AM UTC
        new RecurringJobOptions()
    );
});

app.UseHangfireDashboard("/hangfire");

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
