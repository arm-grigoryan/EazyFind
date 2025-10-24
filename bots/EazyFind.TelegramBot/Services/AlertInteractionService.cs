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
                await botClient.SendTextMessageAsync(chatId, "Ընտրեք խանութները։", cancellationToken: cancellationToken);
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
                await botClient.SendTextMessageAsync(chatId, "Հաստատեք կամ չեղարկեք։", cancellationToken: cancellationToken);
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

        await botClient.SendTextMessageAsync(chatId, "Գրեք բանալի բառեր ծանուցում ստեղծելու համար (առնվազն 3 սիմվոլ)։", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
    }

    private async Task HandleKeywordsAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
        {
            await botClient.SendTextMessageAsync(chatId, "Խնդրում ենք մուտքագրել առնվազն 3 սիմվոլ:", cancellationToken: cancellationToken);
            return;
        }

        session.Keywords = text;
        session.Stage = AlertConversationStage.SelectingStores;
        session.IncludeAllStores = true;
        session.SelectedStores.Clear();

        var prompt = await botClient.SendTextMessageAsync(chatId, "Ընտրեք խանութներ այս ծանուցման որոնումները կատարելու համար", replyMarkup: BuildAlertStoreKeyboard(session), cancellationToken: cancellationToken);
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
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ընտրեք առնվազն 1 խանութ կամ սեղմեք ՝Բոլոր խանութները՝ կոճակը։", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            session.Stage = AlertConversationStage.AwaitingPriceChoice;
            if (session.StoreSelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(chatId, session.StoreSelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
                session.StoreSelectionMessageId = null;
            }

            var keyboard = new ReplyKeyboardMarkup(new[] { new[] { new KeyboardButton("Այո"), new KeyboardButton("Ոչ") } })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(chatId, "Ցանկանու՞մ եք տրամադրել գնային միջակայք", replyMarkup: keyboard, cancellationToken: cancellationToken);
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

        var allLabel = session.IncludeAllStores ? "✅ Բոլոր խանութները" : "Բոլոր խանութները";
        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(allLabel, $"{AlertStorePrefix}all"),
            InlineKeyboardButton.WithCallbackData("Մաքրել", $"{AlertStorePrefix}clear"),
            InlineKeyboardButton.WithCallbackData("Հաստատել", $"{AlertStorePrefix}done")
        });

        return new InlineKeyboardMarkup(rows);
    }

    private async Task HandlePriceChoiceAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (text.Equals("Այո", StringComparison.OrdinalIgnoreCase))
        {
            session.Stage = AlertConversationStage.AwaitingMinPrice;
            await botClient.SendTextMessageAsync(chatId, "Գրեք մինիմում գինը կամ սեղմեք ՝Բաց թողնել՝ կոճակը", replyMarkup: BuildSkipKeyboard(), cancellationToken: cancellationToken);
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
            await botClient.SendTextMessageAsync(chatId, "Գրեք մաքսիմում գինը կամ սեղմեք ՝Բաց թողնել՝ կոճակը", replyMarkup: BuildSkipKeyboard(), cancellationToken: cancellationToken);
            return;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Խնդրում ենք գրեք ոչ բացասական թիվ կամ սեղմեք ՝Բաց թողնել՝ կոճակը", cancellationToken: cancellationToken);
            return;
        }

        session.MinPrice = value;
        session.Stage = AlertConversationStage.AwaitingMaxPrice;
        await botClient.SendTextMessageAsync(chatId, "Գրեք մաքսիմում գինը կամ սեղմեք ՝Բաց թողնել՝ կոճակը", replyMarkup: BuildSkipKeyboard(), cancellationToken: cancellationToken);
    }

    private async Task HandleMaxPriceAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, string text, CancellationToken cancellationToken)
    {
        if (text.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            session.MaxPrice = null;
        }
        else if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Խնդրում ենք գրեք ոչ բացասական թիվ կամ սեղմեք ՝Բաց թողնել՝ կոճակը", cancellationToken: cancellationToken);
            return;
        }
        else
        {
            session.MaxPrice = value;
        }

        if (session.MinPrice.HasValue && session.MaxPrice.HasValue && session.MinPrice > session.MaxPrice)
        {
            await botClient.SendTextMessageAsync(chatId, "Մաքսիմում գինը պետք է մեծ կամ հավասար լինի մինիմում գնից", cancellationToken: cancellationToken);
            return;
        }

        session.Stage = AlertConversationStage.AwaitingConfirmation;
        await ShowAlertSummaryAsync(botClient, chatId, session, cancellationToken);
    }

    private ReplyKeyboardMarkup BuildSkipKeyboard()
    {
        return new ReplyKeyboardMarkup(new[] { new[] { new KeyboardButton("Բաց թողնել") } })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }

    private async Task ShowAlertSummaryAsync(ITelegramBotClient botClient, long chatId, AlertCreationSession session, CancellationToken cancellationToken)
    {
        var summary = new StringBuilder();
        summary.AppendLine("Ծանուցման ամփոփում:");
        summary.AppendLine($"Բանալի բառեր: {session.Keywords}");

        if (session.IncludeAllStores)
        {
            summary.AppendLine("Խանութներ: Բոլոր խանութները");
        }
        else
        {
            var stores = session.SelectedStores.Count == 0
                ? "Ոչինչ ընտրված չէ"
                : string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()));
            summary.AppendLine($"Խանութներ: {stores}");
        }

        if (session.MinPrice.HasValue)
        {
            summary.AppendLine($"Մինիմում գին։ {session.MinPrice:0.##}");
        }

        if (session.MaxPrice.HasValue)
        {
            summary.AppendLine($"Մաքսիմում գին։ {session.MaxPrice:0.##}");
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Ստեղծել ✅", $"{AlertConfirmPrefix}create"),
                InlineKeyboardButton.WithCallbackData("Չեղարկել ❌", $"{AlertConfirmPrefix}cancel")
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
            await botClient.SendTextMessageAsync(chatId, "Ծանուցման ստեղծումը չեղարկվեց։", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
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
            await botClient.SendTextMessageAsync(chatId, "Ծանուցումը հաջողությամբ ստեղծվեց!", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            _stateService.Clear(chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Չհաջողվեց ստեղծել ծանուցումը։ Խնդրում ենք նորից փորձել։", cancellationToken: cancellationToken);
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
            await botClient.SendTextMessageAsync(chatId, "Չհաջողվեց ստանալ ծանուցումները։", cancellationToken: cancellationToken);
            return;
        }

        if (alerts.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Դուք չունեք ծանուցումներ։ Օգտագործեք /alert հրամանը՝ ծանուցում ստեղծելու համար։", cancellationToken: cancellationToken);
            return;
        }

        int i = 1;
        foreach (var alert in alerts)
        {
            var message = BuildAlertDescription(alert, i);
            i++;
            var actionButtons = new[]
            {
                InlineKeyboardButton.WithCallbackData(alert.IsActive ? "Կասեցնել" : "Ակտիվացնել", $"{AlertActionPrefix}{(alert.IsActive ? "disable" : "enable")}:{alert.Id}"),
                InlineKeyboardButton.WithCallbackData("Ջնջել", $"{AlertActionPrefix}delete:{alert.Id}"),
            };

            await botClient.SendTextMessageAsync(chatId, message, replyMarkup: new InlineKeyboardMarkup(new[] { actionButtons }), cancellationToken: cancellationToken);
        }
    }

    private static string BuildAlertDescription(ProductAlert alert, int order)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Ծանուցում #{order}");
        builder.AppendLine($"Բառեր: {alert.SearchText}");
        if (alert.StoreKeys.Count == 0)
        {
            builder.AppendLine("Խանութներ: Բոլոր խանութները");
        }
        else
        {
            var storeNames = alert.StoreKeys
                .Select(key => Enum.TryParse<StoreKey>(key, true, out var parsed) ? parsed.ToDisplayName() : key)
                .ToArray();
            builder.AppendLine($"Խանութներ: {string.Join(", ", storeNames)}");
        }

        if (alert.MinPrice.HasValue)
        {
            builder.AppendLine($"Մինիմում գին։ {alert.MinPrice:0.##}");
        }

        if (alert.MaxPrice.HasValue)
        {
            builder.AppendLine($"Մաքսիմում գին։ {alert.MaxPrice:0.##}");
        }

        builder.AppendLine(alert.IsActive ? "Կարգավիճակ: Ակտիվ" : "Կարգավիճակ: Կասեցված");
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
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ծանուցումն ակտիվացված է։", cancellationToken: cancellationToken);
                    break;
                case "disable":
                    await _alertService.DisableAsync(chatId, alertId, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ծանուցումը կասեցված է։", cancellationToken: cancellationToken);
                    break;
                case "delete":
                    await _alertService.DeleteAsync(chatId, alertId, cancellationToken);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ծանուցումը ջնջված է։", cancellationToken: cancellationToken);
                    break;
                default:
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle alert action {Action} for chat {ChatId}", action, chatId);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Գործողությունը ձախողվեց", cancellationToken: cancellationToken);
        }
    }

    private async Task PauseAlertsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            var alerts = await _alertService.ListAsync(chatId, cancellationToken);
            if (alerts.Count == 0)
            {
                await botClient.SendTextMessageAsync(chatId, "Դուք չունեք ծանուցումներ, կասեցնելու համար։", cancellationToken: cancellationToken);
                return;
            }

            await _alertService.PauseAllAsync(chatId, cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "Բոլոր ծանուցումները կասեցված են։", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause alerts for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Չհաջողվեց կասեցնել ծանուցումները։ Խնդրում ենք նորից փորձել։", cancellationToken: cancellationToken);
        }
    }

    private async Task ResumeAlertsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            var alerts = await _alertService.ListAsync(chatId, cancellationToken);
            if (alerts.Count == 0)
            {
                await botClient.SendTextMessageAsync(chatId, "Դուք չունեք ծանուցումներ, ակտիվացնելու համար։", cancellationToken: cancellationToken);
                return;
            }

            await _alertService.ResumeAllAsync(chatId, cancellationToken);
            await botClient.SendTextMessageAsync(chatId, "Բոլոր ծանուցումներն ակտիվացված են։", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume alerts for chat {ChatId}", chatId);
            await botClient.SendTextMessageAsync(chatId, "Չհաջողվեց ակտիվացնել ծանուցումները։ Խնդրում ենք նորից փորձել։", cancellationToken: cancellationToken);
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
            await botClient.SendTextMessageAsync(chatId, "Չհաջողվեց բեռնել ծանուցումները", cancellationToken: cancellationToken);
            return;
        }

        if (alerts.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Դուք չունեք ծանուցումներ, ջնջելու համար", cancellationToken: cancellationToken);
            return;
        }

        var buttons = alerts
            .Select(alert => new[] { InlineKeyboardButton.WithCallbackData($"#{alert.Id} - {alert.SearchText}", $"{AlertDeletePrefix}{alert.Id}") })
            .ToArray();

        await botClient.SendTextMessageAsync(chatId, "Ընտրեք թե որ ծանուցումն եք ցանկանում ջնջել։", replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: cancellationToken);
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
            await botClient.SendTextMessageAsync(chatId, $"Ծանուցում #{alertId}-ը ջնջված է։", cancellationToken: cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete alert {AlertId} for chat {ChatId}", alertId, chatId);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Չհաջողվեց ջնջել ծանուցումը։ Խնդրում ենք նորից փորձել։", cancellationToken: cancellationToken);
        }
    }
}
