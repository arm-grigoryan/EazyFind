using EazyFind.Application.Messaging;
using EazyFind.Application.Products;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Enums;
using EazyFind.Domain.Extensions;
using EazyFind.TelegramBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
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
    private readonly TelegramBotOptions _botOptions;

    public UpdateHandler(
        ConversationStateService stateService,
        ProductSearchService productSearchService,
        AlertInteractionService alertInteractionService,
        IProductMessageBuilder productMessageBuilder,
        ILogger<UpdateHandler> logger,
        IOptions<TelegramBotOptions> botOptions)
    {
        _stateService = stateService;
        _productSearchService = productSearchService;
        _alertInteractionService = alertInteractionService;
        _productMessageBuilder = productMessageBuilder;
        _logger = logger;
        _botOptions = botOptions.Value;
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
        var normalizedText = text.ToLowerInvariant();

        switch (normalizedText)
        {
            case "/info":
                await SendInfoAsync(botClient, chatId, cancellationToken);
                return;
            case "/support":
                var session = _stateService.Reset(chatId);
                await SendSupportAsync(botClient, chatId, cancellationToken);
                session.Stage = ConversationStage.SupportAwaitingMessage;
                return;
            case "/search":
                var searchSession = _stateService.Reset(chatId);
                await SendSearchGuideAsync(botClient, chatId, cancellationToken);
                searchSession.Stage = ConversationStage.AwaitingSearchText;
                return;
        }

        if (await _alertInteractionService.TryHandleCommandAsync(botClient, message, cancellationToken))
        {
            return;
        }

        if (await _alertInteractionService.TryHandleMessageAsync(botClient, message, cancellationToken))
        {
            return;
        }

        if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("BOT_USAGE | Action=Start | ChatId={ChatId}", chatId);
            _ = _stateService.Reset(chatId);
            await SendWelcomeAsync(botClient, chatId, cancellationToken);
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
                await botClient.SendTextMessageAsync(chatId, "Սեղմեք կոճակներին` խանութներն ընտրելու համար։", cancellationToken: cancellationToken);
                break;
            case ConversationStage.SelectingCategories:
                await botClient.SendTextMessageAsync(chatId, "Սեղմեք կոճակներին` կատեգորիաներն ընտրելու համար", cancellationToken: cancellationToken);
                break;
            case ConversationStage.AwaitingLimit:
                await HandleLimitAsync(botClient, chatId, existingSession, text, cancellationToken);
                break;
            case ConversationStage.Completed:
                await botClient.SendTextMessageAsync(chatId, "Սկսեք նոր որոնում /search հրամանով։", cancellationToken: cancellationToken);
                break;
            case ConversationStage.SupportAwaitingMessage:
                await HandleSupportMessageAsync(botClient, message, existingSession, cancellationToken);
                break;
            default:
                await SendCorrectActionsAsync(botClient, chatId, cancellationToken);
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
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Սկսեք /start հրամանի միջոցով", cancellationToken: cancellationToken);
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
        session.SelectedStores.Clear();
        session.SelectedCategories.Clear();

        if (string.Equals(text, "Բաց թողնել", StringComparison.OrdinalIgnoreCase))
        {
            session.SearchText = null;
            session.Stage = ConversationStage.SelectingCategories;

            await botClient.SendTextMessageAsync(chatId, "Ընտրեք ապրանքների կատեգորիաներ (առնվազն 1 հատ).", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            var prompt = await botClient.SendTextMessageAsync(chatId, "Օգտագործեք կոճակները` կատեգորիաներ ընտրելու համար։ Սեղմեք 'Բոլոր կատեգորիաները' կամ 'Հաստատել' վերջացնելու համար։", replyMarkup: BuildCategoryKeyboard(session), cancellationToken: cancellationToken);
            session.CategorySelectionMessageId = prompt.MessageId;
        }
        else if (text.Length < 3)
        {
            await botClient.SendTextMessageAsync(chatId, "Խնդրում ենք մուտքագրել առնվազն 3 սիմվոլ կամ սեղմել 'Բաց թողնել' կոճակը։", cancellationToken: cancellationToken);
            return;
        }
        else
        {
            session.SearchText = text;
            session.Stage = ConversationStage.SelectingStores;

            _logger.LogInformation("BOT_USAGE | Action=Search | ChatId={ChatId} | Query=\"{Query}\"", chatId, text);

            await botClient.SendTextMessageAsync(chatId, "Ընտրեք խանութները` որոնման համար (առնվազն 1 հատ).", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            var prompt = await botClient.SendTextMessageAsync(chatId, "Օգտագործեք կոճակները` խանութներ ընտրելու համար։ Սեղմեք 'Բոլոր խանութները' կամ 'Հաստատել' վերջացնելու համար։", replyMarkup: BuildStoreKeyboard(session), cancellationToken: cancellationToken);
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
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ընտրեք առնվազն 1 խանութ։", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            session.Stage = ConversationStage.AwaitingLimit;
            if (session.StoreSelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.StoreSelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
            }

            var selectedStores = string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()));
            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Ընտրված խանութներ: {selectedStores}", cancellationToken: cancellationToken);

            var limitKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "5", "10", "20" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "Քանի՞ ապրանք եք ուզում տեսնել (1-25)", replyMarkup: limitKeyboard, cancellationToken: cancellationToken);
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
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ընտրեք առնվազն 1 կատեգորիա։", showAlert: true, cancellationToken: cancellationToken);
                return;
            }

            session.Stage = ConversationStage.SelectingStores;
            if (session.CategorySelectionMessageId.HasValue)
            {
                await botClient.EditMessageReplyMarkupAsync(callbackQuery.Message!.Chat.Id, session.CategorySelectionMessageId.Value, replyMarkup: null, cancellationToken: cancellationToken);
            }

            var selectedCategories = string.Join(", ", session.SelectedCategories.Select(c => c.ToDisplayName()));
            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Ընտրված կատեգորիաներ: {selectedCategories}", cancellationToken: cancellationToken);

            await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "Ընտրեք խանութներ` որոնում կատարելու համար (առնվազն 1 հատ).", cancellationToken: cancellationToken);
            var prompt = await botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, "Օգտագործեք կոճակները` խանութներ ընտրելու համար։ Սեղմեք 'Բոլոր խանութները' կամ 'Հաստատել' վերջացնելու համար։", replyMarkup: BuildStoreKeyboard(session), cancellationToken: cancellationToken);
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
            await botClient.SendTextMessageAsync(chatId, "Խնդրում ենք գրել թիվ 1-25 միջակայքում։", cancellationToken: cancellationToken);
            return;
        }

        session.Take = take;

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"Փնտրում ենք մինչև {take} ապրանքներ...");
        if (!string.IsNullOrWhiteSpace(session.SearchText))
        {
            summaryBuilder.AppendLine($"Որոնման բառեր: {session.SearchText}");
        }

        summaryBuilder.AppendLine($"Խանութներ: {string.Join(", ", session.SelectedStores.Select(s => s.ToDisplayName()))}");

        if (session.HasSelectedCategories)
        {
            summaryBuilder.AppendLine($"Կատեգորիաներ: {string.Join(", ", session.SelectedCategories.Select(c => c.ToDisplayName()))}");
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
            await botClient.SendTextMessageAsync(chatId, "Տեղի ունեցավ սխալ։ Խնդրում ենք փորձել ավելի ուշ։", cancellationToken: cancellationToken);
        }
        else if (response.Items.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Նշված ֆիլտրներով ոչ մի ապրանք չի գտնվել։ Ուզու՞մ եք տեղեկացվել գտնվելու դեպքում՝ Ստեղծեք ծանուցում /alert հրամանով։", cancellationToken: cancellationToken);
        }
        else
        {
            foreach (var product in response.Items)
            {
                await SendProductAsync(botClient, chatId, product, cancellationToken);
            }
        }

        _logger.LogInformation("BOT_USAGE | Action=SearchCompleted | ChatId={ChatId} | Found={FoundCount} | Query=\"{Query}\"",
            chatId, response?.Items?.Count ?? 0, session.SearchText);

        session.Stage = ConversationStage.Completed;
        await botClient.SendTextMessageAsync(chatId, "Որոնումն ավարտվեց։ Օգտագործեք /search հրամանը նոր որոնում սկսելու համար։", cancellationToken: cancellationToken);
    }

    private async Task SendProductAsync(ITelegramBotClient botClient, long chatId, Product product, CancellationToken cancellationToken)
    {
        var message = _productMessageBuilder.Build(product);

        InlineKeyboardMarkup markup = null;
        if (!string.IsNullOrWhiteSpace(message.Url))
        {
            var signature = ComputeHmacSha256(_botOptions.RedirectSecret, message.Url, chatId);
            var redirectUrl = $"https://eazyfind.duckdns.org/api/redirect?url={Uri.EscapeDataString(message.Url)}&chatId={chatId}&sig={signature}";
            markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Տեսնել խանութում", redirectUrl));
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
        await botClient.SendTextMessageAsync(chatId, "Բարի գալուստ 👋", cancellationToken: cancellationToken);
        await SendInfoAsync(botClient, chatId, cancellationToken);
    }

    private static async Task SendSearchGuideAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var skipKeyboard = new ReplyKeyboardMarkup
        ([
            new KeyboardButton[] { "Բաց թողնել" }
        ])
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        var message = new StringBuilder();
        message.AppendLine("Գրեք բառեր որոնման համար կամ սեղմեք 'Բաց թողնել' կոճակը ՝ ֆիլտրներով որոնում կատարելու համար։");

        await botClient.SendTextMessageAsync(chatId, message.ToString(), replyMarkup: skipKeyboard, cancellationToken: cancellationToken);
    }

    private static Task<Message> SendInfoAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var message = new StringBuilder();
        message.AppendLine("EazyFind-ը որոնում է ապրանքներ հայկական օնլայն խանութներում և տեղեկացնում նոր առաջարկների մասին։");
        message.AppendLine();
        message.AppendLine("📦 Կարող եք փնտրել հետևյալ կատեգորիաներում՝");

        var categories = Enum.GetValues<CategoryType>().Select(c => c.ToDisplayName()).ToList();

        var groups = new[]
        {
            new { Title = "💻 Թվային տեխնիկա", Items = new[] { "Հեռախոսներ", "Պլանշետներ", "Նոթբուքներ", "Բոլորը մեկում համակարգիչներ", "Ստացիոնար համակարգիչներ", "Մոնիտորներ", "Հեռուստացույցներ" } },
            new { Title = "🎧 Աքսեսուարներ", Items = new[] { "Ժամացույցներ", "Ականջակալներ", "Մկնիկներ", "Ստեղնաշարեր" } },
            new { Title = "🎮 Խաղային սարքեր", Items = new[] { "Xbox", "Nintendo Switch", "PlayStation" } },
            new { Title = "🏠 Կենցաղային տեխնիկա", Items = new[] { "Օդորակիչներ", "Սառնարաններ", "Ներկառուցվող սառնարաններ", "Side-by-Side սառնարաններ", "Գինու սառնարաններ", "Սառնարանների պարագաներ" } }
        };

        foreach (var group in groups)
        {
            var matching = categories
                .Where(c => group.Items.Any(i => string.Equals(i, c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matching.Count > 0)
            {
                message.AppendLine();
                message.AppendLine(group.Title + ":");
                message.AppendLine("• " + string.Join(", ", matching));
            }
        }

        var groupedNames = groups.SelectMany(g => g.Items).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = categories.Where(c => !groupedNames.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

        if (others.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("📦 Այլ կատեգորիաներ:");
            message.AppendLine("• " + string.Join(", ", others));
        }

        message.AppendLine();
        message.AppendLine("• Որոնեք ակնթարթորեն Ձեր ընտրած խանութներում և կատոգորիաներում ՝ /search հրամանով։");
        message.AppendLine();
        message.AppendLine("• Ստեղծեք անհատական ծանուցումներ /alert հրամանով ՝ նոր առաջարկների մասին անմիջապես տեղեկացվելու համար։");
        message.AppendLine();
        message.AppendLine("• Կասեցրեք, ակտիվացրեք, կամ ջնջեք ծանուցումները ցանկացած պահի։");

        return botClient.SendTextMessageAsync(chatId, message.ToString(), cancellationToken: cancellationToken);
    }

    private async Task HandleSupportMessageAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var user = message.From;
        var username = user?.Username != null ? $"@{user.Username}" : user?.FirstName ?? "Անհայտ օգտատեր";

        // Send to your private support channel
        await botClient.SendTextMessageAsync(
            chatId: _botOptions.SupportChannelId,
            text:
                $"📩 <b>Նոր հաղորդագրություն</b>\n\n" +
                $"👤 <b>From:</b> {username}\n" +
                $"🆔 <b>ChatId:</b> {chatId}\n" +
                $"💬 <b>Message:</b> {message.Text}",
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);

        await botClient.SendTextMessageAsync(chatId,
            "✅ Շնորհակալություն, Ձեր հաղորդագրությունը գրանցված է։",
            cancellationToken: cancellationToken);

        session.Stage = ConversationStage.Completed;
    }

    private static Task<Message> SendCorrectActionsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var message = new StringBuilder();
        message.AppendLine("• Որոնեք ակնթարթորեն Ձեր ընտրած խանութներում և կատոգորիաներում ՝ /search հրամանով։");
        message.AppendLine();
        message.AppendLine("• Ստեղծեք անհատական ծանուցումներ /alert հրամանով ՝ նոր առաջարկների մասին անմիջապես տեղեկացվելու համար։");
        message.AppendLine();
        message.AppendLine("• Կասեցրեք, ակտիվացրեք, կամ ջնջեք ծանուցումները ցանկացած պահի։");

        return botClient.SendTextMessageAsync(chatId, message.ToString(), cancellationToken: cancellationToken);
    }

    private static Task<Message> SendSupportAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var message = new StringBuilder();
        message.AppendLine("Օգնության կարիք ունե՞ք կամ ցանկանու՞մ եք կիսվել առաջարկներով");
        message.AppendLine("Թողեք հաղորդագրություն և մենք կկապվենք Ձեզ հետ։");

        return botClient.SendTextMessageAsync(chatId, message.ToString(), cancellationToken: cancellationToken);
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
            InlineKeyboardButton.WithCallbackData(session.SelectedStores.Count == stores.Length ? "✅ Բոլոր խանութները" : "Բոլոր խանութները", "store:all"),
            InlineKeyboardButton.WithCallbackData("Մաքրել", "store:clear"),
            InlineKeyboardButton.WithCallbackData("Հաստատել", "store:done")
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
            InlineKeyboardButton.WithCallbackData(session.SelectedCategories.Count == categories.Length ? "✅ Բոլոր կատեգորիաները" : "Բոլոր կատեգորիաները", "category:all"),
            InlineKeyboardButton.WithCallbackData("Մաքրել", "category:clear"),
            InlineKeyboardButton.WithCallbackData("Հաստատել", "category:done")
        });

        return new InlineKeyboardMarkup(rows);
    }

    private static string ComputeHmacSha256(string secret, string url, long chatId)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes($"{chatId}:{url}");

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hash);
    }
}