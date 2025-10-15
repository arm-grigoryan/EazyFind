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
}
