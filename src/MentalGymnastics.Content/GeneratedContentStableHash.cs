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

    public static IReadOnlyList<T> OrderByOrdinal<T>(
        IEnumerable<T> items,
        string requestFingerprint,
        string purpose,
        int ordinal,
        Func<T, string> identity)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentNullException.ThrowIfNull(identity);

        var remaining = items
            .Select((item, index) => new
            {
                Item = item,
                Index = index,
                Key = HashSegment(string.Join("|", requestFingerprint, purpose, identity(item))),
            })
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Index)
            .Select(item => item.Item)
            .ToList();
        var ordered = new List<T>(remaining.Count);
        var quotient = ordinal;
        while (remaining.Count > 0)
        {
            var index = quotient % remaining.Count;
            quotient /= remaining.Count;
            ordered.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        return ordered;
    }

    public static int OrdinalIndex(
        string requestFingerprint,
        string purpose,
        int ordinal,
        int length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var hash = HashSegment(string.Join("|", requestFingerprint, purpose));
        var offset = Convert.ToInt32(hash[..6], 16) % length;
        return (offset + ordinal) % length;
    }

    public static int PermutationCount(int itemCount, int selectedCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemCount);
        if (selectedCount < 0 || selectedCount > itemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedCount));
        }

        var result = 1L;
        for (var index = 0; index < selectedCount; index++)
        {
            result *= itemCount - index;
            if (result > int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)result;
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
