using System.Text.Json;
using WorkItemsMcp.Models;

namespace WorkItemsMcp.Services;

public class TaskRepository(VaultService vault, MarkdownTaskSerializer serializer, TaskIdGenerator idGenerator)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public TaskItem Create(CreateTaskInput input)
    {
        var paths = vault.GetPaths();
        var id = idGenerator.Next();

        var task = new TaskItem
        {
            Id = id,
            Title = input.Title,
            Status = input.Status,
            Urgency = input.Urgency,
            Created = input.Date,
            Updated = input.Date,
            Deadline = input.Deadline,
            Blocked = input.Blocked,
            Description = input.Description,
            NextActions = input.NextActions,
            OpenQuestions = input.OpenQuestions,
        };

        var content = serializer.Write(task, input.Date);
        File.WriteAllText(paths.TaskFile(id), content);
        UpdateIndex(task, paths);

        return task;
    }

    public TaskItem? Get(string taskId)
    {
        var paths = vault.GetPaths();
        var file = paths.TaskFile(taskId);
        if (!File.Exists(file)) return null;
        return serializer.Parse(File.ReadAllText(file));
    }

    public (TaskItem? task, string? content) GetWithContent(string taskId)
    {
        var paths = vault.GetPaths();
        var file = paths.TaskFile(taskId);
        if (!File.Exists(file)) return (null, null);
        var content = File.ReadAllText(file);
        return (serializer.Parse(content), content);
    }

    public void SaveContent(string taskId, string content)
    {
        var paths = vault.GetPaths();
        File.WriteAllText(paths.TaskFile(taskId), content);
        var task = serializer.Parse(content);
        UpdateIndex(task, paths);
    }

    public List<TaskItem> List(string? status = null, string? urgency = null, bool? blocked = null)
    {
        var paths = vault.GetPaths();
        if (!Directory.Exists(paths.Tasks)) return [];

        var tasks = new List<TaskItem>();
        foreach (var file in Directory.GetFiles(paths.Tasks, "TASK-*.md"))
        {
            var task = serializer.Parse(File.ReadAllText(file));
            if (status != null && task.Status != status) continue;
            if (urgency != null && task.Urgency != urgency) continue;
            if (blocked.HasValue && task.Blocked != blocked.Value) continue;
            tasks.Add(task);
        }

        return tasks.OrderBy(t => t.Id).ToList();
    }

    public List<SearchMatch> Search(string query)
    {
        var paths = vault.GetPaths();
        if (!Directory.Exists(paths.Tasks)) return [];

        var matches = new List<SearchMatch>();
        var queryLower = query.ToLowerInvariant();

        foreach (var file in Directory.GetFiles(paths.Tasks, "TASK-*.md"))
        {
            var content = File.ReadAllText(file);
            if (!content.ToLowerInvariant().Contains(queryLower)) continue;

            var task = serializer.Parse(content);
            var idx = content.ToLowerInvariant().IndexOf(queryLower, StringComparison.Ordinal);
            var start = Math.Max(0, idx - 50);
            var end = Math.Min(content.Length, idx + 150);
            var snippet = (start > 0 ? "..." : "") + content[start..end].Trim() + (end < content.Length ? "..." : "");

            matches.Add(new SearchMatch
            {
                TaskId = task.Id,
                Title = task.Title,
                Path = $"tasks/{task.Id}.md",
                Snippet = snippet
            });
        }

        return matches;
    }

    private void UpdateIndex(TaskItem task, VaultPaths paths)
    {
        Dictionary<string, object?> index = [];
        if (File.Exists(paths.IndexJson))
        {
            try
            {
                index = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(paths.IndexJson))
                        ?? [];
            }
            catch { index = []; }
        }

        index[task.Id] = new
        {
            id = task.Id,
            title = task.Title,
            status = task.Status,
            urgency = task.Urgency,
            blocked = task.Blocked,
            deadline = task.Deadline,
            created = task.Created,
            updated = task.Updated
        };

        File.WriteAllText(paths.IndexJson, JsonSerializer.Serialize(index, JsonOpts));
    }
}
