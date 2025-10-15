using System.Globalization;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EazyFind.Application.Alerts;

public class AlertEvaluatorService(
    IProductAlertRepository alertRepository,
    IProductAlertMatchRepository alertMatchRepository,
    IAlertEvaluationService alertEvaluationService,
    IAlertNotificationPublisher notificationPublisher,
    IOptions<AlertOptions> options,
    ILogger<AlertEvaluatorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                await EvaluateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alert evaluation loop failed");
            }

            delay = TimeSpan.FromMinutes(Math.Max(1, options.Value.EvaluationMinutes));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        var alerts = await alertRepository.GetActiveAsync(cancellationToken);
        if (alerts.Count == 0)
        {
            return;
        }

        foreach (var alert in alerts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await EvaluateAlertAsync(alert, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate alert {AlertId}", alert.Id);
            }
        }
    }

    private async Task EvaluateAlertAsync(ProductAlert alert, CancellationToken cancellationToken)
    {
        var candidates = await alertEvaluationService.GetCandidatesAsync(alert, 50, cancellationToken);
        var now = DateTime.UtcNow;
        await alertRepository.UpdateLastCheckedAsync(alert.Id, now, cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var max = Math.Max(1, options.Value.MaxNotifiesPerRunPerAlert);
        var toNotify = candidates.Take(max).ToList();
        var remaining = Math.Max(0, candidates.Count - toNotify.Count);

        if (toNotify.Count > 0)
        {
            await notificationPublisher.PublishAsync(alert, toNotify, remaining, cancellationToken);

            var matches = toNotify
                .Select(product => new ProductAlertMatch
                {
                    AlertId = alert.Id,
                    ProductId = product.Id.ToString(CultureInfo.InvariantCulture),
                    MatchedAtUtc = now
                })
                .ToList();

            await alertMatchRepository.BulkInsertAsync(matches, cancellationToken);
        }
    }
}
