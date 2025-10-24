namespace EazyFind.Application.Messaging;

public class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string BotToken { get; set; }
    public long SupportChannelId { get; set; }
}
