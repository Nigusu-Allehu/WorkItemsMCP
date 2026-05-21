using System.Text;
using WorkItemsMcp.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkItemsMcp.Services;

public class MarkdownTaskSerializer
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Generates full markdown content for a new task.</summary>
    public string Write(TaskItem task, string createdDate)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine($"id: {task.Id}");
        sb.AppendLine($"title: \"{EscapeYaml(task.Title)}\"");
        sb.AppendLine($"status: {task.Status}");
        sb.AppendLine($"urgency: {task.Urgency}");
        sb.AppendLine($"created: {task.Created}");
        sb.AppendLine($"updated: {task.Updated}");
        sb.AppendLine($"deadline: {task.Deadline ?? ""}");
        sb.AppendLine($"blocked: {task.Blocked.ToString().ToLower()}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {task.Id} - {task.Title}");
        sb.AppendLine();
        sb.AppendLine("## Description");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(task.Description) ? "" : task.Description);
        sb.AppendLine();
        sb.AppendLine("## Current state");
        sb.AppendLine();
        sb.AppendLine($"Status: {FormatStatus(task.Status)}");
        sb.AppendLine($"Urgency: {FormatUrgency(task.Urgency)}");
        sb.AppendLine($"Blocked: {(task.Blocked ? "Yes" : "No")}");
        sb.AppendLine();
        sb.AppendLine("## Next actions");
        sb.AppendLine();
        foreach (var action in task.NextActions)
            sb.AppendLine($"- [ ] {action}");
        sb.AppendLine();
        sb.AppendLine("## Open questions");
        sb.AppendLine();
        foreach (var q in task.OpenQuestions)
            sb.AppendLine($"- {q}");
        sb.AppendLine();
        sb.AppendLine("## History");
        sb.AppendLine();
        sb.AppendLine($"### {createdDate}");
        sb.AppendLine();
        sb.AppendLine("Created task.");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>Parses YAML front matter into a TaskItem (metadata only; history stays in raw content).</summary>
    public TaskItem Parse(string content)
    {
        var (frontMatter, body) = SplitFrontMatter(content);
        var meta = YamlDeserializer.Deserialize<Dictionary<string, object?>>(frontMatter)
                   ?? new Dictionary<string, object?>();

        return new TaskItem
        {
            Id = S(meta, "id"),
            Title = S(meta, "title"),
            Status = S(meta, "status", "not-started"),
            Urgency = S(meta, "urgency", "normal"),
            Created = S(meta, "created"),
            Updated = S(meta, "updated"),
            Deadline = meta.TryGetValue("deadline", out var dl) && dl != null && dl.ToString() != "" ? dl.ToString() : null,
            Blocked = B(meta, "blocked"),
            Description = GetSection(body, "Description"),
            NextActions = ParseCheckboxList(GetSection(body, "Next actions")),
            OpenQuestions = ParseBulletList(GetSection(body, "Open questions")),
        };
    }

    /// <summary>Appends a new entry under ## History.</summary>
    public string AppendToHistory(string content, string date, string text)
    {
        var entry = $"\n### {date}\n\n{text.TrimEnd()}\n";
        var idx = content.LastIndexOf("\n## History", StringComparison.Ordinal);
        if (idx < 0)
            return content.TrimEnd() + "\n\n## History\n" + entry;
        return content.TrimEnd() + entry;
    }

    /// <summary>Updates specific key/value pairs in YAML front matter.</summary>
    public string UpdateFrontMatter(string content, Dictionary<string, string?> updates)
    {
        var (frontMatter, body) = SplitFrontMatter(content);
        var lines = frontMatter.Split('\n').ToList();

        foreach (var (key, value) in updates)
        {
            var idx = lines.FindIndex(l => l.StartsWith($"{key}:"));
            var newLine = $"{key}: {value ?? ""}";
            if (idx >= 0)
                lines[idx] = newLine;
            else
                lines.Add(newLine);
        }

        return $"---\n{string.Join("\n", lines)}\n---\n{body}";
    }

    /// <summary>Replaces the content of a markdown section (between ## headers).</summary>
    public string UpdateSection(string content, string sectionName, string newContent)
    {
        var lines = content.Split('\n').ToList();
        int start = -1, end = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimEnd() == $"## {sectionName}")
            {
                start = i;
            }
            else if (start >= 0 && i > start && lines[i].StartsWith("## "))
            {
                end = i;
                break;
            }
        }

        if (start < 0) return content;
        if (end < 0) end = lines.Count;

        var result = new List<string>(lines[..(start + 1)]);
        result.Add("");
        result.AddRange(newContent.Split('\n'));
        result.Add("");
        result.AddRange(lines[end..]);
        return string.Join("\n", result);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    public static (string frontMatter, string body) SplitFrontMatter(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---")
            return ("", content);

        int endIdx = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---") { endIdx = i; break; }
        }

        if (endIdx < 0) return ("", content);

        return (string.Join("\n", lines[1..endIdx]), string.Join("\n", lines[(endIdx + 1)..]));
    }

    private static string GetSection(string body, string sectionName)
    {
        var lines = body.Split('\n');
        var sb = new StringBuilder();
        bool inSection = false;

        foreach (var line in lines)
        {
            if (line.TrimEnd() == $"## {sectionName}") { inSection = true; continue; }
            if (inSection && line.StartsWith("## ")) break;
            if (inSection) sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }

    private static List<string> ParseCheckboxList(string section) =>
        section.Split('\n')
               .Where(l => l.TrimStart().StartsWith("- [ ]") || l.TrimStart().StartsWith("- [x]") || l.TrimStart().StartsWith("- [X]"))
               .Select(l => l.TrimStart().Length > 5 ? l.TrimStart()[5..].Trim() : "")
               .Where(s => s != "")
               .ToList();

    private static List<string> ParseBulletList(string section) =>
        section.Split('\n')
               .Where(l => l.TrimStart().StartsWith("- "))
               .Select(l => l.TrimStart()[2..].Trim())
               .ToList();

    private static string S(Dictionary<string, object?> m, string key, string def = "") =>
        m.TryGetValue(key, out var v) && v != null ? v.ToString()! : def;

    private static bool B(Dictionary<string, object?> m, string key) =>
        m.TryGetValue(key, out var v) && v != null && v.ToString()!.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static string EscapeYaml(string s) => s.Replace("\"", "\\\"");

    public static string FormatStatus(string status) => status switch
    {
        "not-started" => "Not Started",
        "in-progress" => "In Progress",
        "blocked"     => "Blocked",
        "done"        => "Done",
        "cancelled"   => "Cancelled",
        _             => status
    };

    public static string FormatUrgency(string urgency) =>
        urgency.Length > 0 ? char.ToUpper(urgency[0]) + urgency[1..] : urgency;
}
