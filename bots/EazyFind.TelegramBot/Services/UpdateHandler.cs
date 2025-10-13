using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.TelegramBot.Extensions;
using EazyFind.TelegramBot.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace EazyFind.TelegramBot.Services;

public class UpdateHandler : IUpdateHandler
{
    private readonly ConversationStateService _stateService;
    private readonly ProductSearchService _productSearchService;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(ConversationStateService stateService, ProductSearchService productSearchService, ILogger<UpdateHandler> logger)
    {
        _stateService = stateService;
        _productSearchService = productSearchService;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message when update.Message is not null:
                    await HandleMessageAsync(botClient, update.Message, cancellationToken);
                    break;
                case UpdateType.CallbackQuery when update.CallbackQuery is not null:
                    await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle update");
        }
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Telegram polling error: {Message}", errorMessage);
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var text = message.Text.Trim();

        if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "/restart", StringComparison.OrdinalIgnoreCase))
        {
            var session = _stateService.Reset(chatId);
            await SendWelcomeAsync(botClient, chatId, cancellationToken);
            session.Stage = ConversationStage.AwaitingSearchText;
            return;
        }

        if (!_stateService.TryGet(chatId, out var existingSession) || existingSession is null)
        {
            existingSession = _stateService.Reset(chatId);
            await SendWelcomeAsync(botClient, chatId, cancellationToken);
            existingSession.Stage = ConversationStage.AwaitingSearchText;
            return;
        }

        switch (existingSession.Stage)
        {
            case ConversationStage.AwaitingSearchText:
                await HandleSearchTextAsync(botClient, chatId, existingSession, text, cancellationToken);
                break;
            case ConversationStage.SelectingStores:
                await botClient.SendTextMessageAsync(chatId, "Please use the buttons to choose stores.", cancellationToken: cancellationToken);
                break;
            case ConversationStage.SelectingCategories:
                await botClient.SendTextMessageAsync(chatId, "Please use the buttons to choose categories.", cancellationToken: cancellationToken);
                break;
            case ConversationStage.AwaitingLimit:
                await HandleLimitAsync(botClient, chatId, existingSession, text, cancellationToken);
                break;
            case ConversationStage.Completed:
                await botClient.SendTextMessageAsync(chatId, "Start a new search with /start.", cancellationToken: cancellationToken);
                break;
            default:
                await SendWelcomeAsync(botClient, chatId, cancellationToken);
                existingSession.Stage = ConversationStage.AwaitingSearchText;
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || callbackQuery.Data is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        if (!_stateService.TryGet(chatId, out var session) || session is null)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Please start with /start", cancellationToken: cancellationToken);
            return;
        }

        if (callbackQuery.Data.StartsWith("store:", StringComparison.Ordinal))
        {
            await HandleStoreSelectionAsync(botClient, callbackQuery, session, cancellationToken);
        }
        else if (callbackQuery.Data.StartsWith("category:", StringComparison.Ordinal))
        {
            await HandleCategorySelectionAsync(botClient, callbackQuery, session, cancellationToken);
        }
    }

    private async Task HandleSearchTextAsync(ITelegramBotClient botClient, long chatId, UserSession session, string text, CancellationToken cancellationToken)
    {
        if (string.Equals(text, "skip", StringComparison.OrdinalIgnoreCase))
        {
            session.SearchText = null;
        }
        else if (text.Length < 3)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter at least 3 characters or type 'skip'.", cancellationToken: cancellationToken);
            return;
        }
        else
        {
            session.SearchText = text;
        }

        session.Stage = ConversationStage.SelectingStores;
        session.SelectedStores.Clear();
        session.SelectedCategories.Clear();

        await botClient.SendTextMessageAsync(chatId, "Select the stores to search in (choose at least one).", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
        var prompt = await botClient.SendTextMessageAsync(chatId, "Use the buttons below to toggle stores. Press 'All stores' or 'Done' when finished.", replyMarkup: BuildStoreKeyboard(session), cancellationToken: cancellationToken);
        session.StoreSelectionMessageId = prompt.MessageId;
    }

    private async Task HandleStoreSelectionAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, UserSession session, CancellationToken cancellationToken)
    {
        var action = callbackQuery.Data!["store:".Length..];
        if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            if (!session.HasSelectedStores)
            {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Select at least one store.", showAlert: true, cancellationToken: cancellationToken);
            return;
        }

            session.Stage = ConversationStage.SelectingCategories;
            if (session.StoreSelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.StoreSelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
            }

            var selectedStores = string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()));
            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Stores selected: {selectedStores}", cancellationToken: cancellationToken);

            var prompt = await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "Select product categories (choose at least one).", replyMarkup: BuildCategoryKeyboard(session), cancellationToken: cancellationToken);
            session.CategorySelectionMessageId = prompt.MessageId;
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        if (action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            session.SelectedStores.Clear();
            foreach (var store in Enum.GetValues<StoreKey>())
            {
                session.SelectedStores.Add(store);
            }
        }
        else if (action.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            session.SelectedStores.Clear();
        }
        else if (Enum.TryParse<StoreKey>(action, out var store))
        {
            if (!session.SelectedStores.Add(store))
            {
                session.SelectedStores.Remove(store);
            }
        }

        if (session.StoreSelectionMessageId.HasValue)
        {
            await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.StoreSelectionMessageId.Value, replyMarkup: BuildStoreKeyboard(session), cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleCategorySelectionAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, UserSession session, CancellationToken cancellationToken)
    {
        var action = callbackQuery.Data!["category:".Length..];
        if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            if (!session.HasSelectedCategories)
            {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Select at least one category.", showAlert: true, cancellationToken: cancellationToken);
            return;
        }

            session.Stage = ConversationStage.AwaitingLimit;
            if (session.CategorySelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.CategorySelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
            }

            var selectedCategories = string.Join(", ", session.SelectedCategories.Select(c => c.ToDisplayName()));
            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Categories selected: {selectedCategories}", cancellationToken: cancellationToken);

            var limitKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "5", "10", "20" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "How many items should be shown? (1-100)", replyMarkup: limitKeyboard, cancellationToken: cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        if (action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            session.SelectedCategories.Clear();
            foreach (var category in Enum.GetValues<CategoryType>())
            {
                session.SelectedCategories.Add(category);
            }
        }
        else if (action.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            session.SelectedCategories.Clear();
        }
        else if (Enum.TryParse<CategoryType>(action, out var category))
        {
            if (!session.SelectedCategories.Add(category))
            {
                session.SelectedCategories.Remove(category);
            }
        }

        if (session.CategorySelectionMessageId.HasValue)
        {
            await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.CategorySelectionMessageId.Value, replyMarkup: BuildCategoryKeyboard(session), cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleLimitAsync(ITelegramBotClient botClient, long chatId, UserSession session, string text, CancellationToken cancellationToken)
    {
        if (!int.TryParse(text, out var take) || take < 1 || take > 100)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter a number between 1 and 100.", cancellationToken: cancellationToken);
            return;
        }

        session.Take = take;

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"Searching for up to {take} products...");
        if (!string.IsNullOrWhiteSpace(session.SearchText))
        {
            summaryBuilder.AppendLine($"Search text: {session.SearchText}");
        }

        summaryBuilder.AppendLine($"Stores: {string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()))}");
        summaryBuilder.AppendLine($"Categories: {string.Join(", ", session.SelectedCategories.Select(c => c.ToDisplayName()))}");

        await botClient.SendTextMessageAsync(chatId, summaryBuilder.ToString(), replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);

        await botClient.SendChatActionAsync(chatId, ChatAction.Typing, cancellationToken);

        var request = new ProductSearchRequest
        {
            ChatId = chatId,
            Skip = 0,
            Take = take,
            Stores = session.SelectedStores.ToArray(),
            Categories = session.SelectedCategories.ToArray(),
            SearchText = session.SearchText
        };

        var response = await _productSearchService.SearchAsync(request, cancellationToken);
        if (response is null)
        {
            await botClient.SendTextMessageAsync(chatId, "There was a problem retrieving products. Please try again later.", cancellationToken: cancellationToken);
        }
        else if (response.Items.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "No products found for the selected filters.", cancellationToken: cancellationToken);
        }
        else
        {
            foreach (var product in response.Items)
            {
                await SendProductAsync(botClient, chatId, product, cancellationToken);
            }
        }

        session.Stage = ConversationStage.Completed;
        await botClient.SendTextMessageAsync(chatId, "Search completed. Use /start to run a new search.", cancellationToken: cancellationToken);
    }

    private async Task SendProductAsync(ITelegramBotClient botClient, long chatId, Product product, CancellationToken cancellationToken)
    {
        var captionBuilder = new StringBuilder();
        captionBuilder.AppendLine($"<b>{Escape(product.Name)}</b>");
        captionBuilder.AppendLine($"Price: {product.Price:0.##}");

        var storeName = product.StoreCategory?.Store?.Name;
        if (!string.IsNullOrWhiteSpace(storeName))
        {
            captionBuilder.AppendLine($"Store: {Escape(storeName)}");
        }
        else if (product.StoreCategory is { StoreKey: var storeKey })
        {
            captionBuilder.AppendLine($"Store: {Escape(storeKey.ToDisplayName())}");
        }

        string? categoryLabel = null;
        if (product.StoreCategory?.Category?.Type is CategoryType resolvedCategory)
        {
            categoryLabel = resolvedCategory.ToDisplayName();
        }
        else if (product.StoreCategory is { CategoryType: var categoryType })
        {
            categoryLabel = categoryType.ToDisplayName();
        }

        if (string.IsNullOrWhiteSpace(categoryLabel) &&
            !string.IsNullOrWhiteSpace(product.StoreCategory?.OriginalCategoryName))
        {
            categoryLabel = product.StoreCategory!.OriginalCategoryName;
        }

        if (!string.IsNullOrWhiteSpace(categoryLabel))
        {
            captionBuilder.AppendLine($"Category: {Escape(categoryLabel)}");
        }

        captionBuilder.AppendLine($"Last synced: {product.LastSyncedAt:G}");

        InlineKeyboardMarkup? markup = null;
        if (!string.IsNullOrWhiteSpace(product.Url))
        {
            markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Open in store", product.Url));
        }

        var caption = captionBuilder.ToString().Trim();

        try
        {
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
            {
                await botClient.SendPhotoAsync(chatId, InputFile.FromUri(product.ImageUrl), caption: caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send photo for product {ProductId}", product.Id);
        }

        await botClient.SendTextMessageAsync(chatId, caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
    }

    private static InlineKeyboardMarkup BuildStoreKeyboard(UserSession session)
    {
        var rows = new List<InlineKeyboardButton[]>();
        var stores = Enum.GetValues<StoreKey>();

        foreach (var chunk in stores.Chunk(2))
        {
            rows.Add(chunk.Select(store =>
            {
                var label = session.SelectedStores.Contains(store) ? $"✅ {store.ToDisplayName()}" : store.ToDisplayName();
                return InlineKeyboardButton.WithCallbackData(label, $"store:{store}");
            }).ToArray());
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(session.SelectedStores.Count == stores.Length ? "✅ All stores" : "All stores", "store:all"),
            InlineKeyboardButton.WithCallbackData("Clear", "store:clear"),
            InlineKeyboardButton.WithCallbackData("Done", "store:done")
        });

        return new InlineKeyboardMarkup(rows);
    }

    private static InlineKeyboardMarkup BuildCategoryKeyboard(UserSession session)
    {
        var rows = new List<InlineKeyboardButton[]>();
        var categories = Enum.GetValues<CategoryType>();

        foreach (var chunk in categories.Chunk(3))
        {
            rows.Add(chunk.Select(category =>
            {
                var label = session.SelectedCategories.Contains(category) ? $"✅ {category.ToDisplayName()}" : category.ToDisplayName();
                return InlineKeyboardButton.WithCallbackData(label, $"category:{category}");
            }).ToArray());
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(session.SelectedCategories.Count == categories.Length ? "✅ All categories" : "All categories", "category:all"),
            InlineKeyboardButton.WithCallbackData("Clear", "category:clear"),
            InlineKeyboardButton.WithCallbackData("Done", "category:done")
        });

        return new InlineKeyboardMarkup(rows);
    }

    private static string Escape(string? value) => string.IsNullOrEmpty(value) ? string.Empty : WebUtility.HtmlEncode(value);

    private static async Task SendWelcomeAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var skipKeyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "Skip" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        var message = new StringBuilder();
        message.AppendLine("Welcome to EazyFind!");
        message.AppendLine("Enter keywords to search for products or tap Skip to search by filters only.");

        await botClient.SendTextMessageAsync(chatId, message.ToString(), replyMarkup: skipKeyboard, cancellationToken: cancellationToken);
    }
}
