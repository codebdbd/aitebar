using System.Text;

namespace AiteBar;

internal static class ClipboardTextTransforms
{
    public static string ToSingleLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        string[] parts = normalized
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return CollapseWhitespace(string.Join(" ", parts));
    }

    public static string ToDisplayText(string text, int maxLength = 72)
    {
        string singleLine = ToSingleLine(text);
        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        int safeLength = maxLength;
        if (safeLength < singleLine.Length && char.IsSurrogatePair(singleLine, safeLength - 1))
        {
            safeLength--;
        }

        return singleLine[..safeLength] + "...";
    }

    public static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int count = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static string CollapseWhitespace(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        bool hasNewlinesOrTabs = false;
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (ch == '\n' || ch == '\r' || ch == '\t')
            {
                hasNewlinesOrTabs = true;
                break;
            }
        }

        if (!hasNewlinesOrTabs)
        {
            return value.Trim();
        }

        var builder = new StringBuilder(value.Length);
        bool previousWasWhitespace = false;

        foreach (char ch in value)
        {
            bool isWhitespace = char.IsWhiteSpace(ch);
            if (isWhitespace)
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }
            }
            else
            {
                builder.Append(ch);
            }

            previousWasWhitespace = isWhitespace;
        }

        return builder.ToString().Trim();
    }
}
