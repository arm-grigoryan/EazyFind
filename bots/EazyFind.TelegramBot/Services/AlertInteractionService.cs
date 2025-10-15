using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using EazyFind.Application.Alerts;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Extensions;
using EazyFind.TelegramBot.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EazyFind.TelegramBot.Services;

public class AlertInteractionService
{
    private const string AlertStorePrefix = "alertstore:";
    private const string AlertConfirmPrefix = "alertconfirm:";
    private const string AlertActionPrefix = "alertaction:";
    private const string AlertDeletePrefix = "alertdelete:";

    private readonly AlertConversationStateService _stateService;
    private readonly AlertBotService _alertService;
    private readonly ILogger<AlertInteractionService> _logger;

    public AlertInteractionService(AlertConversationStateService stateService, AlertBotService alertService, ILogger<AlertInteractionService> logger)
    {
        _stateService = stateService;
        _alertService = alertService;
        _logger = logger;
    }

    public async Task<bool> TryHandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            return false;
        }

        var text = message.Text.Trim();
        var chatId = message.Chat.Id;

        switch (text.ToLowerInvariant())
        {
            case "/alert":
                await StartAlertCreationAsync(botClient, chatId, cancellationToken);
                return true;
            case "/myalerts":
                await ShowAlertsAsync(botClient, chatId, cancellationToken);
                return true;
            case "/pausealerts":
                await PauseAlertsAsync(botClient, chatId, cancellationToken);
                return true;
            case "/resumealerts":
                await ResumeAlertsAsync(botClient, chatId, cancellationToken);
                return true;
            case "/deletealert":
                await StartDeleteAlertAsync(botClient, chatId, cancellationToken);
                return true;
            default:
                return false;
        }
    }

    public async Task<bool> TryHandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            return false;
        }

        var chatId = message.Chat.Id;
        if (!_stateService.TryGet(chatId, out var session) || session is null || session.Stage == AlertConversationStage.None)
        {
            return false;
        }

        var text = message.Text.Trim();
        switch (session.Stage)
        {
            case AlertConversationStage.AwaitingKeywords:
                await HandleKeywordsAsync(botClient, chatId, session, text, cancellationToken);
                return true;
            case AlertConversationStage.SelectingStores:
                await botClient.SendTextMessageAsync(chatId, "Please use the buttons to choose stores.", cancellationToken: cancellationToken);
                return true;
            case AlertConversationStage.AwaitingPriceChoice:
                await HandlePriceChoiceAsync(botClient, chatId, session, text, cancellationToken);
                return true;
            case AlertConversationStage.AwaitingMinPrice:
                await HandleMinPriceAsync(botClient, chatId, session, text, cancellationToken);
                return true;
            case AlertConversationStage.AwaitingMaxPrice:
                await HandleMaxPriceAsync(botClient, chatId, session, text, cancellationToken);
                return true;
            case AlertConversationStage.AwaitingConfirmation:
                await botClient.SendTextMessageAsync(chatId, "Please use the buttons to confirm or cancel.", cancellationToken: cancellationToken);
                return true;
            default:
                return false;
        }
    }

    public async Task<bool> TryHandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null)
        {
            return false;
        }

        var data = callbackQuery.Data;
        var chatId = callbackQuery.Message?.Chat.Id;
        if (chatId is null)
        {
            return false;
        }

        if (data.StartsWith(AlertStorePrefix, StringComparison.Ordinal))
        {
            await HandleStoreCallbackAsync(botClient, callbackQuery, cancellationToken);
            return true;
        }

        if (data.StartsWith(AlertConfirmPrefix, StringComparison.Ordinal))
        {
            await HandleConfirmationCallbackAsync(botClient, callbackQuery, cancellationToken);
            return true;
        }

        if (data.StartsWith(AlertActionPrefix, StringComparison.Ordinal))
        {
            await HandleAlertActionCallbackAsync(botClient, callbackQuery, cancellationToken);
            return true;
        }

        if (data.StartsWith(AlertDeletePrefix, StringComparison.Ordinal))
        {
            await HandleDeleteCallbackAsync(botClient, callbackQuery, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task StartAlertCreationAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        _stateService.Clear(chatId);
        var session = _stateService.GetOrCreate(chatId);
        session.Stage = AlertConversationStage.AwaitingKeywords;
        session.Keywords = string.Empty;
        session.SelectedStores.Clear();
        session.IncludeAllStores = true;
        session.MinPrice = null;
        session.MaxPrice = null;
        session.StoreSelectionMessageId = null;

        await botClient.SendTextMessageAsync(chatId, "Enter keywords for the alert (at least 2 characters).", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
    }

    private async Task HandleKeywordsAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter at least 2 characters.", cancellationToken: cancellationToken);
            return;
        }

        session.Keywords = text;
        session.Stage = AlertConversationStage.SelectingStores;
        session.IncludeAllStores = true;
        session.SelectedStores.Clear();

        var prompt = await botClient.SendTextMessageAsync(chatId, "Select stores for this alert.", replyMarkup: BuildAlertStoreKeyboard(session), cancellationToken: cancellationToken);
        session.StoreSelectionMessageId = prompt.MessageId;
    }

    private async Task HandleStoreCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        if (!_stateService.TryGet(chatId, out var session) || session is null)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Start with /alert", cancellationToken: cancellationToken);
            return;
        }

        if (session.Stage != AlertConversationStage.SelectingStores)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var action = callbackQuery.Data![AlertStorePrefix.Length..];
        if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            if (!session.IncludeAllStores && session.SelectedStores.Count == 0)
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Select at least one store or choose All stores.", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            session.Stage = AlertConversationStage.AwaitingPriceChoice;
            if (session.StoreSelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(chatId, session.StoreSelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
                session.StoreSelectionMessageId = null;
            }

            var keyboard = new ReplyKeyboardMarkup(new[] { new[] { new KeyboardButton("Yes"), new KeyboardButton("Skip") } })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(chatId, "Set price range? (Yes/Skip)", replyMarkup: keyboard, cancellationToken: cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        if (action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            session.IncludeAllStores = true;
            session.SelectedStores.Clear();
        }
        else if (action.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            session.IncludeAllStores = false;
            session.SelectedStores.Clear();
        }
        else if (Enum.TryParse<StoreKey>(action, true, out var store))
        {
            session.IncludeAllStores = false;
            if (!session.SelectedStores.Add(store))
            {
                session.SelectedStores.Remove(store);
            }
        }

        if (session.StoreSelectionMessageId.HasValue)
        {
            await botClient.EditMessageReplyMarkupAsync(chatId, session.StoreSelectionMessageId.Value, replyMarkup: BuildAlertStoreKeyboard(session), cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private InlineKeyboardMarkup BuildAlertStoreKeyboard(AlertCreationSession session)
    {
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var chunk in Enum.GetValues<StoreKey>().Chunk(2))
        {
            rows.Add(chunk.Select(store =>
            {
                var isSelected = session.IncludeAllStores ? false : session.SelectedStores.Contains(store);
                var label = isSelected ? $"✅ {store.ToDisplayName()}" : store.ToDisplayName();
                return InlineKeyboardButton.WithCallbackData(label, $"{AlertStorePrefix}{store}");
            }).ToArray());
        }

        var allLabel = session.IncludeAllStores ? "✅ All stores" : "All stores";
        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(allLabel, $"{AlertStorePrefix}all"),
            InlineKeyboardButton.WithCallbackData("Clear", $"{AlertStorePrefix}clear"),
            InlineKeyboardButton.WithCallbackData("Done", $"{AlertStorePrefix}done")
        });

        return new InlineKeyboardMarkup(rows);
    }

    private async Task HandlePriceChoiceAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (text.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            session.Stage = AlertConversationStage.AwaitingMinPrice;
            await botClient.SendTextMessageAsync(chatId, "Enter minimum price or type Skip.", replyMarkup: BuildSkipKeyboard(), cancellationToken: cancellationToken);
            return;
        }

        session.MinPrice = null;
        session.MaxPrice = null;
        session.Stage = AlertConversationStage.AwaitingConfirmation;
        await ShowAlertSummaryAsync(botClient, chatId, session, cancellationToken);
    }

    private async Task HandleMinPriceAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (text.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            session.MinPrice = null;
            session.Stage = AlertConversationStage.AwaitingMaxPrice;
            await botClient.SendTextMessageAsync(chatId, "Enter maximum price or type Skip.", replyMarkup: BuildSkipKeyboard(), cancellationToken: cancellationToken);
            return;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter a valid non-negative number or type Skip.", cancellationToken: cancellationToken);
            return;
        }

        session.MinPrice = value;
        session.Stage = AlertConversationStage.AwaitingMaxPrice;
        await botClient.SendTextMessageAsync(chatId, "Enter maximum price or type Skip.", replyMarkup: BuildSkipKeyboard(), cancellationToken: cancellationToken);
    }

    private async Task HandleMaxPriceAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (text.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            session.MaxPrice = null;
        }
        else if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter a valid non-negative number or type Skip.", cancellationToken: cancellationToken);
            return;
        }
        else
        {
            session.MaxPrice = value;
        }

        if (session.MinPrice.HasValue && session.MaxPrice.HasValue && session.MinPrice > session.MaxPrice)
        {
            await botClient.SendTextMessageAsync(chatId, "Maximum price must be greater than or equal to minimum price.", cancellationToken: cancellationToken);
            return;
        }

        session.Stage = AlertConversationStage.AwaitingConfirmation;
        await ShowAlertSummaryAsync(botClient, chatId, session, cancellationToken);
    }

    private ReplyKeyboardMarkup BuildSkipKeyboard()
    {
        return new ReplyKeyboardMarkup(new[] { new[] { new KeyboardButton("Skip") } })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }

    private async Task ShowAlertSummaryAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, CancellationToken cancellationToken)
    {
        var summary = new StringBuilder();
        summary.AppendLine("Alert summary:");
        summary.AppendLine($"Keywords: {session.Keywords}");

        if (session.IncludeAllStores)
        {
            summary.AppendLine("Stores: All stores");
        }
        else
        {
            var stores = session.SelectedStores.Count == 0
                ? "None selected"
                : string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()));
            summary.AppendLine($"Stores: {stores}");
        }

        if (session.MinPrice.HasValue)
        {
            summary.AppendLine($"Min price: {session.MinPrice:0.##}");
        }

        if (session.MaxPrice.HasValue)
        {
            summary.AppendLine($"Max price: {session.MaxPrice:0.##}");
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Create ✅", $"{AlertConfirmPrefix}create"),
                InlineKeyboardButton.WithCallbackData("Cancel ❌", $"{AlertConfirmPrefix}cancel")
            }
        });

        await botClient.SendTextMessageAsync(chatId, summary.ToString(), replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleConfirmationCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        if (!_stateService.TryGet(chatId, out var session) || session is null)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var action = callbackQuery.Data![AlertConfirmPrefix.Length..];
        if (action.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            _stateService.Clear(chatId);
            await botClient.EditMessageReplyMarkupAsync(chatId, callbackQuery.Message.MessageId, replyMarkup: null, cancellationToken: cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "Alert creation cancelled.", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        try
        {
            var storeKeys = session.IncludeAllStores
                ? Array.Empty<string>()
                : session.SelectedStores.Select(s => s.ToString()).ToArray();

            var request = new AlertCreateRequest(chatId, session.Keywords, storeKeys, session.MinPrice, session.MaxPrice);
            await _alertService.CreateAsync(request, cancellationToken);
            await botClient.EditMessageReplyMarkupAsync(chatId, callbackQuery.Message.MessageId, replyMarkup: null, cancellationToken: cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "Alert created successfully!", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            _stateService.Clear(chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Failed to create alert. Please try again.", cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task ShowAlertsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductAlert> alerts;
        try
        {
            alerts = await _alertService.ListAsync(chatId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list alerts for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Unable to retrieve alerts right now.", cancellationToken: cancellationToken);
            return;
        }

        if (alerts.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "You have no alerts. Use /alert to create one.", cancellationToken: cancellationToken);
            return;
        }

        foreach (var alert in alerts)
        {
            var message = BuildAlertDescription(alert);
            var actionButtons = new[]
            {
                InlineKeyboardButton.WithCallbackData(alert.IsActive ? "Disable" : "Enable", $"{AlertActionPrefix}{(alert.IsActive ? "disable" : "enable")}:{alert.Id}"),
                InlineKeyboardButton.WithCallbackData("Delete", $"{AlertActionPrefix}delete:{alert.Id}"),
                InlineKeyboardButton.WithCallbackData("Details", $"{AlertActionPrefix}details:{alert.Id}")
            };

            await botClient.SendTextMessageAsync(chatId, message, replyMarkup: new InlineKeyboardMarkup(new[] { actionButtons }), cancellationToken: cancellationToken);
        }
    }

    private static string BuildAlertDescription(ProductAlert alert)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Alert #{alert.Id}");
        builder.AppendLine($"Keywords: {alert.SearchText}");
        if (alert.StoreKeys.Count == 0)
        {
            builder.AppendLine("Stores: All stores");
        }
        else
        {
            var storeNames = alert.StoreKeys
                .Select(key => Enum.TryParse<StoreKey>(key, true, out var parsed) ? parsed.ToDisplayName() : key)
                .ToArray();
            builder.AppendLine($"Stores: {string.Join(", ", storeNames)}");
        }

        if (alert.MinPrice.HasValue)
        {
            builder.AppendLine($"Min price: {alert.MinPrice:0.##}");
        }

        if (alert.MaxPrice.HasValue)
        {
            builder.AppendLine($"Max price: {alert.MaxPrice:0.##}");
        }

        builder.AppendLine(alert.IsActive ? "Status: Active" : "Status: Paused");
        return builder.ToString();
    }

    private async Task HandleAlertActionCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var parts = callbackQuery.Data![AlertActionPrefix.Length..].Split(':', 2);
        if (parts.Length != 2)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var action = parts[0];
        if (!long.TryParse(parts[1], out var alertId))
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            switch (action)
            {
                case "enable":
                    await _alertService.EnableAsync(chatId, alertId, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Alert enabled.", cancellationToken: cancellationToken);
                    break;
                case "disable":
                    await _alertService.DisableAsync(chatId, alertId, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Alert disabled.", cancellationToken: cancellationToken);
                    break;
                case "delete":
                    await _alertService.DeleteAsync(chatId, alertId, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Alert deleted.", cancellationToken: cancellationToken);
                    break;
                case "details":
                    await ShowAlertDetailsAsync(botClient, chatId, alertId, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
                    break;
                default:
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle alert action {Action} for chat {ChatId}", action, chatId);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Operation failed", cancellationToken: cancellationToken);
        }
    }

    private async Task ShowAlertDetailsAsync(ITelegramBotClient botClient, long chatId, long alertId, CancellationToken cancellationToken)
    {
        try
        {
            var alert = await _alertService.GetAsync(chatId, alertId, cancellationToken);
            if (alert is null)
            {
                await botClient.SendTextMessageAsync(chatId, "Alert not found.", cancellationToken: cancellationToken);
                return;
            }

            var details = BuildAlertDescription(alert);
            await botClient.SendTextMessageAsync(chatId, details, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load alert details for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Could not retrieve alert details.", cancellationToken: cancellationToken);
        }
    }

    private async Task PauseAlertsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            await _alertService.PauseAllAsync(chatId, cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "All alerts paused.", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause alerts for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Unable to pause alerts right now.", cancellationToken: cancellationToken);
        }
    }

    private async Task ResumeAlertsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            await _alertService.ResumeAllAsync(chatId, cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "All alerts resumed.", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume alerts for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Unable to resume alerts right now.", cancellationToken: cancellationToken);
        }
    }

    private async Task StartDeleteAlertAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductAlert> alerts;
        try
        {
            alerts = await _alertService.ListAsync(chatId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list alerts for deletion for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Unable to load alerts right now.", cancellationToken: cancellationToken);
            return;
        }

        if (alerts.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "You have no alerts to delete.", cancellationToken: cancellationToken);
            return;
        }

        var buttons = alerts
            .Select(alert => new[] { InlineKeyboardButton.WithCallbackData($"#{alert.Id} - {alert.SearchText}", $"{AlertDeletePrefix}{alert.Id}") })
            .ToArray();

        await botClient.SendTextMessageAsync(chatId, "Select an alert to delete:", replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: cancellationToken);
    }

    private async Task HandleDeleteCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (!long.TryParse(callbackQuery.Data![AlertDeletePrefix.Length..], out var alertId))
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            await _alertService.DeleteAsync(chatId, alertId, cancellationToken);
            await botClient.EditMessageReplyMarkupAsync(chatId, callbackQuery.Message.MessageId, replyMarkup: null, cancellationToken: cancellationToken);
            await botClient.SendTextMessageAsync(chatId, $"Alert #{alertId} deleted.", cancellationToken: cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete alert {AlertId} for chat {ChatId}", alertId, chatId);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Delete failed", cancellationToken: cancellationToken);
        }
    }
}
