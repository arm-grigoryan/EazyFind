using EazyFind.Application;
using EazyFind.Infrastructure;
using EazyFind.Jobs.Configuration;
using EazyFind.Jobs.Extensions;
using EazyFind.Jobs.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, configuration) =>
{
    var connectionString = context.Configuration.GetConnectionString("HangfireDatabase");

    configuration.WriteTo.PostgreSQL(connectionString, "hangfire.logs", needAutoCreateTable: true)
        .MinimumLevel.Information()
        .Filter.ByExcluding(logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var path) &&
            (path.ToString().Contains("/hangfire") ||
             path.ToString().Contains("/metrics") ||
             path.ToString().Contains("/_"))
        );

    if (!context.HostingEnvironment.IsProduction())
    {
        configuration.WriteTo.Console()
        .MinimumLevel.Information()
        .Filter.ByExcluding(logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var path) &&
            (path.ToString().Contains("/hangfire") ||
             path.ToString().Contains("/metrics") ||
             path.ToString().Contains("/_"))
        );
    }
});

services.Configure<CategoryConfigs>(configuration.GetSection(nameof(CategoryConfigs)));
services.Configure<ScraperConfigs>(configuration.GetSection(nameof(ScraperConfigs)));
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

GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 });

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
    var options = scope.ServiceProvider.GetRequiredService<IOptions<JobConfigs>>();

    foreach (var categorySchedule in options.Value.CategorySchedules)
    {
        recurringJobs.AddOrUpdate(
            $"{categorySchedule.Key}-Job",
            () => jobService.RunScrapersAsync(categorySchedule.Key),
            categorySchedule.Value,
            new RecurringJobOptions()
        );
    }
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new AllowAllDashboardAuthorizationFilter()]
});


app.MapGet("/", () => "EazyFind.Jobs API is running!");


//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}