using EazyFind.Application.Products;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Extensions;
using EazyFind.TelegramBot.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EazyFind.TelegramBot.Services;

public class UpdateHandler : IUpdateHandler
{
    private readonly ConversationStateService _stateService;
    private readonly ProductSearchService _productSearchService;
    private readonly AlertInteractionService _alertInteractionService;
    private readonly IProductMessageBuilder _productMessageBuilder;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(ConversationStateService stateService, ProductSearchService productSearchService, AlertInteractionService alertInteractionService, IProductMessageBuilder productMessageBuilder, ILogger<UpdateHandler> logger)
    {
        _stateService = stateService;
        _productSearchService = productSearchService;
        _alertInteractionService = alertInteractionService;
        _productMessageBuilder = productMessageBuilder;
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

        if (await _alertInteractionService.TryHandleCommandAsync(botClient, message, cancellationToken))
        {
            return;
        }

        if (await _alertInteractionService.TryHandleMessageAsync(botClient, message, cancellationToken))
        {
            return;
        }

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

        if (await _alertInteractionService.TryHandleCallbackAsync(botClient, callbackQuery, cancellationToken))
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

    private static async Task HandleSearchTextAsync(ITelegramBotClient botClient, long chatId, UserSession session, string text, CancellationToken cancellationToken)
    {
        session.SelectedStores.Clear();
        session.SelectedCategories.Clear();

        if (string.Equals(text, "skip", StringComparison.OrdinalIgnoreCase))
        {
            session.SearchText = null;
            session.Stage = ConversationStage.SelectingCategories;

            await botClient.SendTextMessageAsync(chatId, "Select product categories (choose at least one).", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            var prompt = await botClient.SendTextMessageAsync(chatId, "Use the buttons below to toggle categories. Press 'All categories' or 'Done' when finished.", replyMarkup: BuildCategoryKeyboard(session), cancellationToken: cancellationToken);
            session.CategorySelectionMessageId = prompt.MessageId;
        }
        else if (text.Length < 3)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter at least 3 characters or type 'skip'.", cancellationToken: cancellationToken);
            return;
        }
        else
        {
            session.SearchText = text;
            session.Stage = ConversationStage.SelectingStores;

            await botClient.SendTextMessageAsync(chatId, "Select the stores to search in (choose at least one).", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            var prompt = await botClient.SendTextMessageAsync(chatId, "Use the buttons below to toggle stores. Press 'All stores' or 'Done' when finished.", replyMarkup: BuildStoreKeyboard(session), cancellationToken: cancellationToken);
            session.StoreSelectionMessageId = prompt.MessageId;
        }
    }

    private static async Task HandleStoreSelectionAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, UserSession session, CancellationToken cancellationToken)
    {
        var action = callbackQuery.Data!["store:".Length..];
        if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            if (!session.HasSelectedStores)
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Select at least one store.", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            session.Stage = ConversationStage.AwaitingLimit;
            if (session.StoreSelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.StoreSelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
            }

            var selectedStores = string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()));
            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Stores selected: {selectedStores}", cancellationToken: cancellationToken);

            var limitKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "5", "10", "20" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "How many items should be shown? (1-25)", replyMarkup: limitKeyboard, cancellationToken: cancellationToken);
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

    private static async Task HandleCategorySelectionAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, UserSession session, CancellationToken cancellationToken)
    {
        var action = callbackQuery.Data!["category:".Length..];
        if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            if (!session.HasSelectedCategories)
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Select at least one category.", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            session.Stage = ConversationStage.SelectingStores;
            if (session.CategorySelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.CategorySelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
            }

            var selectedCategories = string.Join(", ", session.SelectedCategories.Select(c => c.ToDisplayName()));
            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Categories selected: {selectedCategories}", cancellationToken: cancellationToken);

            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "Now, select the stores to search in (choose at least one).", cancellationToken: cancellationToken);
            var prompt = await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "Use the buttons below to toggle stores. Press 'All stores' or 'Done' when finished.", replyMarkup: BuildStoreKeyboard(session), cancellationToken: cancellationToken);
            session.StoreSelectionMessageId = prompt.MessageId;

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
        if (!int.TryParse(text, out var take) || take < 1 || take > 25)
        {
            await botClient.SendTextMessageAsync(chatId, "Please enter a number between 1 and 25.", cancellationToken: cancellationToken);
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

        if (session.HasSelectedCategories)
        {
            summaryBuilder.AppendLine($"Categories: {string.Join(", ", session.SelectedCategories.Select(c => c.ToDisplayName()))}");
        }

        await botClient.SendTextMessageAsync(chatId, summaryBuilder.ToString(), replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);

        await botClient.SendChatActionAsync(chatId, ChatAction.Typing, null, cancellationToken);

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
        var message = _productMessageBuilder.Build(product);

        InlineKeyboardMarkup? markup = null;
        if (!string.IsNullOrWhiteSpace(message.Url))
        {
            markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Open in store", message.Url));
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(message.PhotoUrl))
            {
                await botClient.SendPhotoAsync(chatId, InputFile.FromUri(message.PhotoUrl), caption: message.Caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send photo for product {ProductId}", product.Id);
        }

        await botClient.SendTextMessageAsync(chatId, message.Caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
    }

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
}