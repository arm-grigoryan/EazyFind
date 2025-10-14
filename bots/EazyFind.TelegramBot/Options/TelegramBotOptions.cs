namespace EazyFind.TelegramBot.Options;

public class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";
    public const string PlaceholderToken = "YOUR_TELEGRAM_BOT_TOKEN";

    public string BotToken { get; set; } = PlaceholderToken;
}
