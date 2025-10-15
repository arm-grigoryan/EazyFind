using System;
using System.Collections.Generic;
using EazyFind.Application.Alerts;
using EazyFind.Application.Products;
using EazyFind.Domain.Entities;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace EazyFind.API.Services;

internal class TelegramAlertNotificationPublisher(
    ITelegramBotClient botClient,
    IProductMessageBuilder messageBuilder,
    ILogger<TelegramAlertNotificationPublisher> logger) : IAlertNotificationPublisher
{
    public async Task PublishAsync(ProductAlert alert, IReadOnlyList<Product> products, int remainingCount, CancellationToken cancellationToken)
    {
        foreach (var product in products)
        {
            var message = messageBuilder.Build(product);
            InlineKeyboardMarkup? markup = null;
            if (!string.IsNullOrWhiteSpace(message.Url))
            {
                markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Open in store", message.Url));
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(message.PhotoUrl))
                {
                    await botClient.SendPhotoAsync(alert.ChatId, InputFile.FromUri(message.PhotoUrl), caption: message.Caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                    continue;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send alert photo for product {ProductId}", product.Id);
            }

            await botClient.SendTextMessageAsync(alert.ChatId, message.Caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
        }

        if (remainingCount > 0)
        {
            await botClient.SendTextMessageAsync(alert.ChatId, $"+{remainingCount} more matches found. Use /myalerts to review.", cancellationToken: cancellationToken);
        }
    }
}
