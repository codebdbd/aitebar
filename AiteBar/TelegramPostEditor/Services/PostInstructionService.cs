using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using Microsoft.Data.Sqlite;

namespace AiteBar.TelegramPostEditor.Services;

public sealed record ComposedInstructionResult(
    string SystemPrompt,
    IReadOnlyList<string> AppliedSteps,
    IReadOnlyList<string> ConflictWarnings,
    string? ChannelInstructionVersionId,
    string? PostInstructionVersionId);

public sealed class PostInstructionService
{
    public const string SafetyAndAccuracyRule =
        "Strict accuracy rule: Do not invent unverified facts, fake sources, statistics, quotes, or arbitrary URLs. Preserve all user-provided links and factual details unless asked to remove them. Write in the same language as the user's text unless explicitly asked to translate. Return ONLY the final ready-to-publish Telegram post text with no preamble, no explanations, and no meta-labels such as 'Greeting:', 'Summary:', 'Приветствие:', 'Условие:', or 'Итог:'.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly PostDbMigrator _migrator;

    public PostInstructionService(PostDbMigrator migrator)
    {
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _migrator.EnsureMigrated();
    }

    public void EnsureBuiltInInstructions()
    {
        if (GetAll().Count > 0)
        {
            return;
        }

        Save(new PostInstruction
        {
            Id = "builtin-news-brief",
            Title = "Краткая новость",
            Category = "News",
            Body = "Пиши кратко, по делу: 1-2 коротких абзаца, ключевой факт в первом предложении, без воды."
        });

        Save(new PostInstruction
        {
            Id = "builtin-expert-review",
            Title = "Экспертный разбор",
            Category = "Analytics",
            Body = "Структурируй пост на тезисы и выводы, сохраняй профессиональный тон и ясные переходы."
        });

        Save(new PostInstruction
        {
            Id = "builtin-digest",
            Title = "Деловой дайджест",
            Category = "Digest",
            Body = "Оформи материал как компактный дайджест с маркированными пунктами и чётким итогом."
        });
    }

    public PostInstruction Save(PostInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        if (string.IsNullOrWhiteSpace(instruction.Id))
        {
            instruction.Id = Guid.NewGuid().ToString("N");
        }

        DateTime now = DateTime.UtcNow;
        if (instruction.CreatedUtc == default)
        {
            instruction.CreatedUtc = now;
        }
        instruction.UpdatedUtc = now;

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        int latestVersion = GetLatestVersion(connection, transaction, instruction.Id);
        string? latestBody = latestVersion > 0 ? GetVersionBody(connection, transaction, instruction.Id, latestVersion) : null;

        if (latestVersion == 0)
        {
            instruction.CurrentVersion = 1;
        }
        else if (!string.Equals(latestBody, instruction.Body, StringComparison.Ordinal))
        {
            instruction.CurrentVersion = latestVersion + 1;
        }
        else
        {
            instruction.CurrentVersion = latestVersion;
        }

        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO instructions (id, title, category, current_version, preferred_connection_id, preferred_model_id, body, created_utc, updated_utc)
                VALUES ($id, $title, $category, $version, $connId, $modelId, $body, $created, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    category = excluded.category,
                    current_version = excluded.current_version,
                    preferred_connection_id = excluded.preferred_connection_id,
                    preferred_model_id = excluded.preferred_model_id,
                    body = excluded.body,
                    updated_utc = excluded.updated_utc;
                """;
            cmd.Parameters.AddWithValue("$id", instruction.Id);
            cmd.Parameters.AddWithValue("$title", instruction.Title.Trim());
            cmd.Parameters.AddWithValue("$category", string.IsNullOrWhiteSpace(instruction.Category) ? "General" : instruction.Category.Trim());
            cmd.Parameters.AddWithValue("$version", instruction.CurrentVersion);
            cmd.Parameters.AddWithValue("$connId", (object?)instruction.PreferredConnectionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$modelId", (object?)instruction.PreferredModelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$body", instruction.Body ?? string.Empty);
            cmd.Parameters.AddWithValue("$created", instruction.CreatedUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$updated", instruction.UpdatedUtc.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        if (latestVersion == 0 || !string.Equals(latestBody, instruction.Body, StringComparison.Ordinal))
        {
            using SqliteCommand verCmd = connection.CreateCommand();
            verCmd.Transaction = transaction;
            verCmd.CommandText = """
                INSERT INTO instruction_versions (id, instruction_id, version, body, created_utc)
                VALUES ($id, $instId, $version, $body, $created);
                """;
            verCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            verCmd.Parameters.AddWithValue("$instId", instruction.Id);
            verCmd.Parameters.AddWithValue("$version", instruction.CurrentVersion);
            verCmd.Parameters.AddWithValue("$body", instruction.Body ?? string.Empty);
            verCmd.Parameters.AddWithValue("$created", now.ToString("O"));
            verCmd.ExecuteNonQuery();
        }

        transaction.Commit();
        return instruction;
    }

    public PostInstruction? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, category, current_version, preferred_connection_id, preferred_model_id, body, created_utc, updated_utc
            FROM instructions
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);

        using SqliteDataReader reader = cmd.ExecuteReader();
        return reader.Read() ? ReadInstruction(reader) : null;
    }

    public IReadOnlyList<PostInstruction> GetAll(string? searchText = null, string? category = null)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();

        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(category))
        {
            where.Add("category = $category");
            cmd.Parameters.AddWithValue("$category", category.Trim());
        }
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            where.Add("(title LIKE $search OR body LIKE $search)");
            cmd.Parameters.AddWithValue("$search", $"%{searchText.Trim()}%");
        }

        string whereClause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT id, title, category, current_version, preferred_connection_id, preferred_model_id, body, created_utc, updated_utc
            FROM instructions
            {whereClause}
            ORDER BY title ASC;
            """;

        var list = new List<PostInstruction>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadInstruction(reader));
        }
        return list;
    }

    public IReadOnlyList<PostInstructionVersion> GetVersions(string instructionId)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, instruction_id, version, body, created_utc
            FROM instruction_versions
            WHERE instruction_id = $instId
            ORDER BY version DESC;
            """;
        cmd.Parameters.AddWithValue("$instId", instructionId);

        var list = new List<PostInstructionVersion>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PostInstructionVersion
            {
                Id = reader.GetString(0),
                InstructionId = reader.GetString(1),
                Version = reader.GetInt32(2),
                Body = reader.GetString(3),
                CreatedUtc = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }
        return list;
    }

    public PostInstructionVersion? GetLatestVersionRecord(string instructionId) =>
        GetVersions(instructionId).FirstOrDefault();

    public PostInstruction RestoreVersion(string instructionId, int versionNumber)
    {
        PostInstruction current = GetById(instructionId)
            ?? throw new KeyNotFoundException($"Instruction '{instructionId}' not found.");
        PostInstructionVersion targetVersion = GetVersions(instructionId).FirstOrDefault(v => v.Version == versionNumber)
            ?? throw new KeyNotFoundException($"Version {versionNumber} for instruction '{instructionId}' not found.");

        current.Body = targetVersion.Body;
        return Save(current);
    }

    public PostInstruction Duplicate(string instructionId, string? newTitle = null)
    {
        PostInstruction source = GetById(instructionId)
            ?? throw new KeyNotFoundException($"Instruction '{instructionId}' not found.");

        return Save(new PostInstruction
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(newTitle) ? $"{source.Title} (Copy)" : newTitle.Trim(),
            Category = source.Category,
            PreferredConnectionId = source.PreferredConnectionId,
            PreferredModelId = source.PreferredModelId,
            Body = source.Body
        });
    }

    public bool Delete(string instructionId)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM instructions WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", instructionId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public string ExportToJson(PostInstruction instruction) =>
        JsonSerializer.Serialize(instruction, JsonOptions);

    public PostInstruction ImportFromJson(string json)
    {
        PostInstruction imported = JsonSerializer.Deserialize<PostInstruction>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid instruction JSON.");
        imported.Id = Guid.NewGuid().ToString("N");
        imported.CurrentVersion = 1;
        imported.CreatedUtc = DateTime.UtcNow;
        return Save(imported);
    }

    public string ExportToMarkdown(PostInstruction instruction)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(instruction.Title);
        sb.Append("Category: ").AppendLine(instruction.Category);
        sb.Append("Version: ").AppendLine(instruction.CurrentVersion.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.AppendLine(instruction.Body);
        return sb.ToString();
    }

    public ComposedInstructionResult ComposePrompt(
        string? channelInstructionId,
        string? postInstructionId,
        string? userTask)
    {
        var steps = new List<string> { "1. Safety & accuracy rules" };
        var sb = new StringBuilder();
        sb.AppendLine(SafetyAndAccuracyRule);

        string? channelVersionId = null;
        string? postVersionId = null;

        PostInstruction? channelInst = !string.IsNullOrWhiteSpace(channelInstructionId) ? GetById(channelInstructionId) : null;
        PostInstruction? postInst = !string.IsNullOrWhiteSpace(postInstructionId) ? GetById(postInstructionId) : null;

        if (channelInst != null)
        {
            channelVersionId = GetLatestVersionRecord(channelInst.Id)?.Id;
            steps.Add($"2. Channel style: {channelInst.Title} (v{channelInst.CurrentVersion})");
            sb.AppendLine().AppendLine($"[Channel style — {channelInst.Title} (v{channelInst.CurrentVersion})]").AppendLine(channelInst.Body);
        }

        if (postInst != null && postInst.Id != channelInst?.Id)
        {
            postVersionId = GetLatestVersionRecord(postInst.Id)?.Id;
            steps.Add($"{steps.Count + 1}. Post instruction: {postInst.Title} (v{postInst.CurrentVersion})");
            sb.AppendLine().AppendLine($"[Post instruction — {postInst.Title} (v{postInst.CurrentVersion})]").AppendLine(postInst.Body);
        }

        if (!string.IsNullOrWhiteSpace(userTask))
        {
            steps.Add($"{steps.Count + 1}. User task");
            sb.AppendLine().AppendLine("[User task]").AppendLine(userTask.Trim());
        }

        List<string> conflicts = DetectConflicts(channelInst?.Body, postInst?.Body, userTask);
        return new ComposedInstructionResult(sb.ToString().Trim(), steps, conflicts, channelVersionId, postVersionId);
    }

    private static List<string> DetectConflicts(params string?[] segments)
    {
        string combined = string.Join("\n", segments.Where(s => !string.IsNullOrWhiteSpace(s))).ToLowerInvariant();
        var warnings = new List<string>();

        if ((combined.Contains("кратко", StringComparison.Ordinal) || combined.Contains("short", StringComparison.Ordinal)) &&
            (combined.Contains("подробн", StringComparison.Ordinal) || combined.Contains("detailed", StringComparison.Ordinal) || combined.Contains("longread", StringComparison.Ordinal)))
        {
            warnings.Add("Conflicting length directives detected (short vs detailed).");
        }

        if ((combined.Contains("без эмодзи", StringComparison.Ordinal) || combined.Contains("no emoji", StringComparison.Ordinal)) &&
            (combined.Contains("с эмодзи", StringComparison.Ordinal) || combined.Contains("используй эмодзи", StringComparison.Ordinal) || combined.Contains("use emoji", StringComparison.Ordinal)))
        {
            warnings.Add("Conflicting emoji directives detected (no emoji vs use emoji).");
        }

        return warnings;
    }

    private static int GetLatestVersion(SqliteConnection connection, SqliteTransaction transaction, string instructionId)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM instruction_versions WHERE instruction_id = $id;";
        cmd.Parameters.AddWithValue("$id", instructionId);
        object? result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static string? GetVersionBody(SqliteConnection connection, SqliteTransaction transaction, string instructionId, int version)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT body FROM instruction_versions WHERE instruction_id = $id AND version = $ver;";
        cmd.Parameters.AddWithValue("$id", instructionId);
        cmd.Parameters.AddWithValue("$ver", version);
        return cmd.ExecuteScalar() as string;
    }

    private static PostInstruction ReadInstruction(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        Category = reader.GetString(2),
        CurrentVersion = reader.GetInt32(3),
        PreferredConnectionId = reader.IsDBNull(4) ? null : reader.GetString(4),
        PreferredModelId = reader.IsDBNull(5) ? null : reader.GetString(5),
        Body = reader.GetString(6),
        CreatedUtc = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        UpdatedUtc = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };
}
