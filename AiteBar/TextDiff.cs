using System.Text.RegularExpressions;

namespace AiteBar;

internal enum TextDiffKind
{
    Unchanged,
    Added,
    Removed
}

internal sealed record TextDiffSegment(TextDiffKind Kind, string Text);

internal static partial class TextDiff
{
    private const int LookAhead = 32;

    public static IReadOnlyList<TextDiffSegment> Create(string original, string changed)
    {
        string[] left = TokenRegex().Matches(original ?? string.Empty).Select(match => match.Value).ToArray();
        string[] right = TokenRegex().Matches(changed ?? string.Empty).Select(match => match.Value).ToArray();
        var result = new List<TextDiffSegment>();
        int leftIndex = 0;
        int rightIndex = 0;

        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (string.Equals(left[leftIndex], right[rightIndex], StringComparison.Ordinal))
            {
                Append(result, TextDiffKind.Unchanged, left[leftIndex]);
                leftIndex++;
                rightIndex++;
                continue;
            }

            (int leftOffset, int rightOffset) = FindNextMatch(left, leftIndex, right, rightIndex);
            if (leftOffset < 0)
            {
                Append(result, TextDiffKind.Removed, left[leftIndex++]);
                Append(result, TextDiffKind.Added, right[rightIndex++]);
                continue;
            }

            for (int i = 0; i < leftOffset; i++)
            {
                Append(result, TextDiffKind.Removed, left[leftIndex++]);
            }
            for (int i = 0; i < rightOffset; i++)
            {
                Append(result, TextDiffKind.Added, right[rightIndex++]);
            }
        }

        while (leftIndex < left.Length)
        {
            Append(result, TextDiffKind.Removed, left[leftIndex++]);
        }
        while (rightIndex < right.Length)
        {
            Append(result, TextDiffKind.Added, right[rightIndex++]);
        }
        return result;
    }

    private static (int LeftOffset, int RightOffset) FindNextMatch(
        string[] left,
        int leftIndex,
        string[] right,
        int rightIndex)
    {
        int leftLimit = Math.Min(LookAhead, left.Length - leftIndex);
        int rightLimit = Math.Min(LookAhead, right.Length - rightIndex);
        for (int distance = 1; distance < leftLimit + rightLimit; distance++)
        {
            for (int leftOffset = 0; leftOffset <= distance && leftOffset < leftLimit; leftOffset++)
            {
                int rightOffset = distance - leftOffset;
                if (rightOffset >= rightLimit)
                {
                    continue;
                }
                if (string.Equals(
                    left[leftIndex + leftOffset],
                    right[rightIndex + rightOffset],
                    StringComparison.Ordinal))
                {
                    return (leftOffset, rightOffset);
                }
            }
        }
        return (-1, -1);
    }

    private static void Append(List<TextDiffSegment> result, TextDiffKind kind, string text)
    {
        if (result.Count > 0 && result[^1].Kind == kind)
        {
            TextDiffSegment previous = result[^1];
            result[^1] = previous with { Text = previous.Text + text };
        }
        else
        {
            result.Add(new TextDiffSegment(kind, text));
        }
    }

    [GeneratedRegex(@"\s+|[^\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
