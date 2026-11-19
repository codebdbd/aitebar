using System.Text;
using System.Text.RegularExpressions;

namespace AiteBar;

internal enum SettingsListKind
{
    Ai,
    Iptv,
    TelegramBots,
    TelegramReading,
    TelegramPublishing,
    Rss,
    Prompts
}

public sealed record SettingsSourceEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public string Category { get; init; } = "";
    public bool IsEnabled { get; init; } = true;
}

internal static partial class SettingsListTextFormat
{
    internal sealed record Row(int Line, string[] Values);

    public static string[] Columns(SettingsListKind kind) => kind switch
    {
        SettingsListKind.Ai => ["Name", "Provider", "URL", "Token", "Enabled"],
        SettingsListKind.Iptv => ["Name", "URL or path", "Type", "Enabled"],
        SettingsListKind.TelegramBots => ["Name", "Telegram ID", "Token"],
        SettingsListKind.TelegramReading => ["Name", "Link or Telegram ID", "Category"],
        SettingsListKind.TelegramPublishing => ["Name", "Link or Telegram ID", "Bot", "Category"],
        SettingsListKind.Rss => ["Name", "RSS URL", "Category", "Enabled"],
        SettingsListKind.Prompts => ["Name", "Prompt", "Category"],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string Write(SettingsListKind kind, IEnumerable<string[]> values, bool markdown)
    {
        string[] columns = Columns(kind);
        var result = new StringBuilder();
        Append(columns);
        if (markdown) Append(columns.Select(_ => "---"));
        foreach (var row in values) Append(row);
        return result.ToString();

        void Append(IEnumerable<string> row)
        {
            string cells = string.Join(markdown ? " | " : "\t", row.Select(value => Escape(value, markdown)));
            result.AppendLine(markdown ? $"| {cells} |" : cells);
        }
    }

    public static IReadOnlyList<Row> Read(SettingsListKind kind, string text)
    {
        string[] columns = Columns(kind);
        string[] lines = text.TrimStart('\uFEFF').ReplaceLineEndings("\n").Split('\n');
        var rows = new List<Row>();
        bool first = true;
        bool markdown = false;
        int[]? order = null;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("```", StringComparison.Ordinal)) continue;
            if (first) markdown = line.TrimStart().StartsWith('|');
            if (markdown && !line.TrimStart().StartsWith('|')) continue;
            string[] values = markdown ? ReadMarkdownCells(line) : line.Split('\t').Select(value => Unescape(value, false)).ToArray();
            if (kind == SettingsListKind.Iptv && !markdown && values.Length == 1 &&
                TryReadIptvCommaPair(line, columns, first, out string[] pair))
            {
                values = pair;
            }
            if (first)
            {
                first = false;
                if (values.Any(IsHeader) && values.All(value => ColumnIndex(columns, value) >= 0))
                {
                    order = values.Select(value => ColumnIndex(columns, value)).ToArray();
                    continue;
                }
            }
            if (markdown && values.All(value => IsMarkdownSeparator().IsMatch(value))) continue;
            if (kind is SettingsListKind.TelegramReading or SettingsListKind.TelegramPublishing or SettingsListKind.Rss &&
                order is null && values.Length == 1)
            {
                values = ["", values[0]];
            }
            if (order is not null)
            {
                var canonical = new string[columns.Length];
                Array.Fill(canonical, "");
                for (int cell = 0; cell < Math.Min(values.Length, order.Length); cell++)
                {
                    if (order[cell] >= 0) canonical[order[cell]] = values[cell];
                }
                values = canonical;
            }
            rows.Add(new Row(index + 1, values));
        }
        return rows;
    }

    private static bool TryReadIptvCommaPair(string line, string[] columns, bool first, out string[] values)
    {
        values = [];
        int comma = line.IndexOf(',');
        if (comma <= 0) return false;
        string name = line[..comma].Trim();
        string location = line[(comma + 1)..].Trim();
        bool isHeader = first && IsHeader(name) && ColumnIndex(columns, location) == 1;
        bool isWebAddress = Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https";
        if (!isHeader && !isWebAddress) return false;
        values = [name, location];
        return true;
    }

    private static bool IsHeader(string value) => value.Trim().ToLowerInvariant() is
        "name" or "title" or "название" or "заголовок" or "назва" or "titel";

    private static int ColumnIndex(string[] columns, string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        int exactIndex = Array.FindIndex(columns, column => column.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (exactIndex >= 0) return exactIndex;
        if (IsHeader(normalized)) return 0;
        string canonical = normalized switch
        {
            "провайдер" or "anbieter" => "Provider",
            "токен" or "api-ключ" => "Token",
            "тип" or "typ" => "Type",
            "категория" or "категорія" or "kategorie" => "Category",
            "бот" => "Bot",
            "текст промпта" or "текст промпту" => "Prompt",
            "включено" or "увімкнено" or "aktiv" => "Enabled",
            _ => ""
        };
        if (canonical.Length > 0) return Array.IndexOf(columns, canonical);
        if (normalized is "ссылка" or "ссылка rss" or "ссылка или telegram id" or "ссылка или путь" or
            "посилання" or "посилання rss" or "link")
        {
            return Array.FindIndex(columns, column => column is "URL" or "RSS URL" or "URL or path" or "Link or Telegram ID");
        }
        return -1;
    }

    private static string Escape(string value, bool markdown)
    {
        string encoded = value.Contains('\\') || value.Contains('"') || value != value.Trim() || value.IndexOfAny(['\t', '\r', '\n']) >= 0
            ? JsonSerializer.Serialize(value) : value;
        return markdown ? encoded.Replace("|", "\\|", StringComparison.Ordinal) : encoded;
    }

    private static string[] ReadMarkdownCells(string line)
    {
        string content = line.Trim();
        var cells = new List<string>();
        int start = 1;
        for (int index = start; index < content.Length; index++)
        {
            if (content[index] == '\\' && index + 1 < content.Length)
            {
                index++;
                continue;
            }
            if (content[index] == '|')
            {
                cells.Add(Unescape(content[start..index].Trim(), true));
                start = index + 1;
            }
        }
        if (start < content.Length || cells.Count == 0) cells.Add(Unescape(content[start..].Trim(), true));
        return [.. cells];
    }

    private static string Unescape(string value, bool markdown)
    {
        string decoded = markdown ? value.Replace("\\|", "|", StringComparison.Ordinal) : value;
        if (decoded.Length >= 2 && decoded[0] == '"' && decoded[^1] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize<string>(decoded) ?? "";
            }
            catch (JsonException) { }
        }
        return decoded;
    }

    [GeneratedRegex("^:?-{3,}:?$")]
    private static partial Regex IsMarkdownSeparator();
}
