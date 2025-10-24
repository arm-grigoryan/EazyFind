using EazyFind.Domain.Enums;
using System.Text.RegularExpressions;

namespace EazyFind.Domain.Extensions;

public static partial class EnumExtensions
{
    public static string ToDisplayName(this Enum value)
    {
        var input = value.ToString();
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var withSpaces = PascalCaseRegex().Replace(input, "$1 $2");
        return withSpaces.Replace("_", " ");
    }

    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalCaseRegex();

    public static string ToDisplayName(this CategoryType category)
    {
        return category switch
        {
            CategoryType.Laptops => "Նոթբուքներ",
            CategoryType.Smartphones => "Հեռախոսներ",
            CategoryType.Monitors => "Մոնիտորներ",
            CategoryType.TVs => "Հեռուստացույցներ",
            CategoryType.Tablets => "Պլանշետներ",
            CategoryType.Watches => "Ժամացույցներ",
            CategoryType.AllInOneComputers => "Բոլորը մեկում համակարգիչներ",
            CategoryType.StationaryComputers => "Ստացիոնար համակարգիչներ",
            CategoryType.Xbox => "Xbox",
            CategoryType.NintendoSwitch => "Nintendo Switch",
            CategoryType.PlayStation => "PlayStation",
            CategoryType.Headsets => "Ականջակալներ",
            CategoryType.Mice => "Մկնիկներ",
            CategoryType.Keyboards => "Ստեղնաշարեր",
            CategoryType.AirConditioners => "Օդորակիչներ",
            CategoryType.Refrigerators => "Սառնարաններ",
            CategoryType.BuiltInRefrigerators => "Ներկառուցվող սառնարաններ",
            CategoryType.SideBySideRefrigerators => "Side-by-Side սառնարաններ",
            CategoryType.WineRefrigerators => "Գինու սառնարաններ",
            CategoryType.RefrigeratorsAccessories => "Սառնարանների պարագաներ",
            _ => category.ToString()
        };
    }
}