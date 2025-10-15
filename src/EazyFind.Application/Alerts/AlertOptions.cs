namespace EazyFind.Application.Alerts;

public class AlertOptions
{
    public const string SectionName = "AlertOptions";

    public int EvaluationMinutes { get; set; } = 10;

    public int MaxNotifiesPerRunPerAlert { get; set; } = 5;
}
