using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

internal static class GeneratedContentStableHash
{
    public static string BuildLoadContextFingerprint(IEnumerable<LoadVariable> loadVariables)
    {
        ArgumentNullException.ThrowIfNull(loadVariables);

        var builder = new StringBuilder();
        foreach (var loadVariable in loadVariables
            .OrderBy(variable => variable.Name, StringComparer.Ordinal)
            .ThenBy(variable => variable.Value, StringComparer.Ordinal))
        {
            AppendLengthPrefixed(builder, "load");
            AppendLengthPrefixed(builder, loadVariable.Name);
            AppendLengthPrefixed(builder, loadVariable.Value);
        }

        return HashSegment(builder.ToString());
    }

    public static string HashSegment(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..24];
    }

    public static void AppendLengthPrefixed(StringBuilder builder, string value)
    {
        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');
    }
}
