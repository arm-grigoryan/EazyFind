using EazyFind.Domain.Enums;

namespace EazyFind.Jobs.Jobs;

internal class CategoryInferrer : ICategoryInferrer
{
    private static readonly Dictionary<CategoryType, HashSet<string>> KeywordMap = new()
    {
        [CategoryType.Headsets] = ["headset", "headphone", "noise", "jbl", "bud", " ear ", "earphone", "in-ear", "over-ear", "tour", "tune", "beam", "sony", "bose", "haylou", "pods", "soundcore", "anker", "marshall", "tws", "beats"],
        [CategoryType.Mice] = ["mouse", "mice", "m185", "m170", "m720", "g203", "g305", "razer viper", "logitech m", "logitech g", "canyon", "pulsefire", "glorious model o", "xtrfy", "steelseries rival", "corsair harpoon", "dpiswitch"],
        [CategoryType.Keyboards] = ["keyboard", "kxx", "kb", "rapoo", "redragon", "mk", "mech"],
        [CategoryType.PlayStation] = ["playstation", "sony"],
        [CategoryType.Xbox] = ["xbox"],
        [CategoryType.NintendoSwitch] = ["nintendo", "switch"]
    };

    public CategoryType? InferCategoryFromName(string name)
    {
        foreach (var (category, keywords) in KeywordMap)
        {
            if (keywords.Any(k => name.Contains(k, StringComparison.InvariantCultureIgnoreCase)))
                return category;
        }

        return default;
    }
}
