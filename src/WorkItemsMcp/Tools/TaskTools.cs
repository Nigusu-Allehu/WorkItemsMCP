using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WorkItemsMcp.Models;
using WorkItemsMcp.Services;

namespace WorkItemsMcp.Tools;

[McpServerToolType]
public class TaskTools(
    VaultService vaultService,
    TaskRepository repository,
    DailyNoteService dailyNotes,
    ViewBuilder viewBuilder,
    MarkdownTaskSerializer serializer)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly string[] ValidStatuses = ["not-started", "in-progress", "blocked", "done", "cancelled"];
    private static readonly string[] ValidUrgencies = ["low", "normal", "high", "urgent"];

    // ── create_task ──────────────────────────────────────────────────────────

    [McpServerTool(Name = "create_task")]
    [Description("Creates a new task file in the vault. Returns the generated task ID.")]
    public string CreateTask(
        [Description("Task title")] string title,
        [Description("Task description")] string? description = null,
        [Description("Status: not-started | in-progress | blocked | done | cancelled")] string status = "not-started",
        [Description("Urgency: low | normal | high | urgent")] string urgency = "normal",
        [Description("Deadline date in YYYY-MM-DD format")] string? deadline = null,
        [Description("Whether the task is currently blocked")] bool blocked = false,
        [Description("Next action items as a list of strings")] string[]? nextActions = null,
        [Description("Open questions as a list of strings")] string[]? openQuestions = null,
        [Description("Today's date in YYYY-MM-DD format (defaults to today)")] string? date = null)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;
        if (!ValidateStatus(status, out var statusErr)) return statusErr!;
        if (!ValidateUrgency(urgency, out var urgencyErr)) return urgencyErr!;

        date ??= Today();

        try
        {
            var task = repository.Create(new CreateTaskInput
            {
                Title = title,
                Description = description ?? "",
                Status = status,
                Urgency = urgency,
                Deadline = string.IsNullOrWhiteSpace(deadline) ? null : deadline,
                Blocked = blocked,
                NextActions = nextActions?.ToList() ?? [],
                OpenQuestions = openQuestions?.ToList() ?? [],
                Date = date,
            });

            dailyNotes.AppendNewTask(date, task.Id, task.Title);
            viewBuilder.Rebuild();

            return JsonSerializer.Serialize(new { status = "created", taskId = task.Id, taskPath = $"tasks/{task.Id}.md" }, JsonOpts);
        }
        catch (Exception ex)
        {
            return VaultTools.Error("io_error", ex.Message);
        }
    }

    // ── get_task ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_task")]
    [Description("Reads one task by its ID and returns its metadata and full markdown content.")]
    public string GetTask(
        [Description("Task ID, e.g. TASK-0001")] string taskId)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        var (task, content) = repository.GetWithContent(taskId);
        if (task == null)
            return VaultTools.Error("task_not_found", $"Task {taskId} was not found.");

        return JsonSerializer.Serialize(new
        {
            status = "found",
            task = new
            {
                id = task.Id,
                title = task.Title,
                status = task.Status,
                urgency = task.Urgency,
                blocked = task.Blocked,
                deadline = task.Deadline,
                path = $"tasks/{task.Id}.md",
                content
            }
        }, JsonOpts);
    }

    // ── update_task ──────────────────────────────────────────────────────────

    [McpServerTool(Name = "update_task")]
    [Description("Updates task metadata and/or content sections. Appends a history entry. Rebuilds views.")]
    public string UpdateTask(
        [Description("Task ID, e.g. TASK-0001")] string taskId,
        [Description("New status value")] string? status = null,
        [Description("New urgency value")] string? urgency = null,
        [Description("New blocked state (omit to leave unchanged)")] bool? blocked = null,
        [Description("New deadline in YYYY-MM-DD format")] string? deadline = null,
        [Description("New description text")] string? description = null,
        [Description("Replacement next action items")] string[]? nextActions = null,
        [Description("Replacement open questions")] string[]? openQuestions = null,
        [Description("Human-readable note to add to history")] string? note = null,
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;
        if (status != null && !ValidateStatus(status, out var statusErr)) return statusErr!;
        if (urgency != null && !ValidateUrgency(urgency, out var urgencyErr)) return urgencyErr!;

        date ??= Today();

        var (task, rawContent) = repository.GetWithContent(taskId);
        if (task == null || rawContent == null)
            return VaultTools.Error("task_not_found", $"Task {taskId} was not found.");

        try
        {
            var content = rawContent;

            // Update front matter
            var fmUpdates = new Dictionary<string, string?> { ["updated"] = date };
            if (status != null)  fmUpdates["status"] = status;
            if (urgency != null) fmUpdates["urgency"] = urgency;
            if (blocked.HasValue) fmUpdates["blocked"] = blocked.Value.ToString().ToLower();
            if (deadline != null) fmUpdates["deadline"] = deadline;
            content = serializer.UpdateFrontMatter(content, fmUpdates);

            // Update content sections
            if (description != null)
                content = serializer.UpdateSection(content, "Description", description);

            if (nextActions != null)
                content = serializer.UpdateSection(content, "Next actions",
                    string.Join("\n", nextActions.Select(a => $"- [ ] {a}")));

            if (openQuestions != null)
                content = serializer.UpdateSection(content, "Open questions",
                    string.Join("\n", openQuestions.Select(q => $"- {q}")));

            // Update Current state
            if (status != null || urgency != null || blocked.HasValue)
            {
                var st  = status  ?? task.Status;
                var urg = urgency ?? task.Urgency;
                var blk = blocked ?? task.Blocked;
                content = serializer.UpdateSection(content, "Current state",
                    $"Status: {MarkdownTaskSerializer.FormatStatus(st)}\n" +
                    $"Urgency: {MarkdownTaskSerializer.FormatUrgency(urg)}\n" +
                    $"Blocked: {(blk ? "Yes" : "No")}");
            }

            // Append history
            var historyNote = note ?? BuildChangeNote(status, urgency, blocked, deadline, description, nextActions, openQuestions);
            content = serializer.AppendToHistory(content, date, historyNote);

            repository.SaveContent(taskId, content);
            dailyNotes.AppendTaskUpdate(date, taskId, note);
            viewBuilder.Rebuild();

            return JsonSerializer.Serialize(new { status = "updated", taskId }, JsonOpts);
        }
        catch (Exception ex)
        {
            return VaultTools.Error("io_error", ex.Message);
        }
    }

    // ── append_task_update ───────────────────────────────────────────────────

    [McpServerTool(Name = "append_task_update")]
    [Description("Adds a history note to a task without modifying any structured fields.")]
    public string AppendTaskUpdate(
        [Description("Task ID, e.g. TASK-0001")] string taskId,
        [Description("Text to add to the task history")] string text,
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        date ??= Today();

        var paths = vaultService.GetPaths();
        var file = paths.TaskFile(taskId);
        if (!File.Exists(file))
            return VaultTools.Error("task_not_found", $"Task {taskId} was not found.");

        try
        {
            var content = File.ReadAllText(file);
            content = serializer.UpdateFrontMatter(content, new() { ["updated"] = date });
            content = serializer.AppendToHistory(content, date, text);
            File.WriteAllText(file, content);

            // Also update index
            repository.SaveContent(taskId, content);
            dailyNotes.AppendTaskUpdate(date, taskId, text);

            return JsonSerializer.Serialize(new { status = "appended", taskId }, JsonOpts);
        }
        catch (Exception ex)
        {
            return VaultTools.Error("io_error", ex.Message);
        }
    }

    // ── list_tasks ───────────────────────────────────────────────────────────

    [McpServerTool(Name = "list_tasks")]
    [Description("Lists tasks, optionally filtered by status, urgency, or blocked state.")]
    public string ListTasks(
        [Description("Filter by status (not-started | in-progress | blocked | done | cancelled)")] string? status = null,
        [Description("Filter by urgency (low | normal | high | urgent)")] string? urgency = null,
        [Description("Filter by blocked: 'true' or 'false' (omit for no filter)")] string? blocked = null)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        bool? blockedFilter = blocked switch
        {
            "true"  => true,
            "false" => false,
            _       => null
        };

        var tasks = repository.List(status, urgency, blockedFilter);

        return JsonSerializer.Serialize(new
        {
            status = "ok",
            tasks = tasks.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                status = t.Status,
                urgency = t.Urgency,
                blocked = t.Blocked,
                deadline = t.Deadline,
                updated = t.Updated,
                path = $"tasks/{t.Id}.md"
            })
        }, JsonOpts);
    }

    // ── search_tasks ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "search_tasks")]
    [Description("Performs a simple text search across all task markdown files.")]
    public string SearchTasks(
        [Description("Search query string")] string query)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        var matches = repository.Search(query);

        return JsonSerializer.Serialize(new
        {
            status = "ok",
            matches = matches.Select(m => new { taskId = m.TaskId, title = m.Title, path = m.Path, snippet = m.Snippet })
        }, JsonOpts);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Today() => DateTime.Today.ToString("yyyy-MM-dd");

    private static bool ValidateStatus(string s, out string? errorJson)
    {
        if (ValidStatuses.Contains(s)) { errorJson = null; return true; }
        errorJson = VaultTools.Error("invalid_status",
            $"Invalid status '{s}'. Valid values: {string.Join(", ", ValidStatuses)}.");
        return false;
    }

    private static bool ValidateUrgency(string u, out string? errorJson)
    {
        if (ValidUrgencies.Contains(u)) { errorJson = null; return true; }
        errorJson = VaultTools.Error("invalid_urgency",
            $"Invalid urgency '{u}'. Valid values: {string.Join(", ", ValidUrgencies)}.");
        return false;
    }

    private static string BuildChangeNote(string? status, string? urgency, bool? blocked,
        string? deadline, string? description, string[]? nextActions, string[]? openQuestions)
    {
        var parts = new List<string>();
        if (status != null)      parts.Add($"Status → {status}.");
        if (urgency != null)     parts.Add($"Urgency → {urgency}.");
        if (blocked.HasValue)    parts.Add($"Blocked → {blocked.Value}.");
        if (deadline != null)    parts.Add($"Deadline → {deadline}.");
        if (description != null) parts.Add("Description updated.");
        if (nextActions != null) parts.Add("Next actions updated.");
        if (openQuestions != null) parts.Add("Open questions updated.");
        return parts.Count > 0 ? string.Join(" ", parts) : "Task updated.";
    }
}
