using System;
using System.Collections.Generic;
using EazyFind.Application.Alerts;
using EazyFind.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EazyFind.TelegramBot.Services;

public class AlertBotService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertBotService> _logger;

    public AlertBotService(IServiceScopeFactory scopeFactory, ILogger<AlertBotService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<ProductAlert> CreateAsync(AlertCreateRequest request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAlertService>();
        return service.CreateAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<ProductAlert>> ListAsync(long chatId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAlertService>();
        return service.ListAsync(chatId, cancellationToken);
    }

    public Task<ProductAlert> GetAsync(long chatId, long alertId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAlertService>();
        return service.GetAsync(chatId, alertId, cancellationToken);
    }

    public Task EnableAsync(long chatId, long alertId, CancellationToken cancellationToken) => ExecuteAsync(s => s.EnableAsync(chatId, alertId, cancellationToken));

    public Task DisableAsync(long chatId, long alertId, CancellationToken cancellationToken) => ExecuteAsync(s => s.DisableAsync(chatId, alertId, cancellationToken));

    public Task DeleteAsync(long chatId, long alertId, CancellationToken cancellationToken) => ExecuteAsync(s => s.DeleteAsync(chatId, alertId, cancellationToken));

    public Task PauseAllAsync(long chatId, CancellationToken cancellationToken) => ExecuteAsync(s => s.PauseAllAsync(chatId, cancellationToken));

    public Task ResumeAllAsync(long chatId, CancellationToken cancellationToken) => ExecuteAsync(s => s.ResumeAllAsync(chatId, cancellationToken));

    private async Task ExecuteAsync(Func<IAlertService, Task> action)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAlertService>();
            await action(service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alert operation failed");
            throw;
        }
    }
}
