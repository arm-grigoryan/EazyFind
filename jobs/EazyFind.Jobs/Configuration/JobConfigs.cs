using EazyFind.Domain.Enums;

namespace EazyFind.Jobs.Configuration;

public class JobConfigs
{
    public Dictionary<CategoryType, string> CategorySchedules { get; set; }
}
