using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WorkItemsMcp.Services;

namespace WorkItemsMcp.Tools;

[McpServerToolType]
public class DailyTools(
    VaultService vaultService,
    DailyNoteService dailyNoteService,
    TaskRepository repository)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [McpServerTool(Name = "resume_day")]
    [Description("Creates or loads today's daily note and returns active, urgent, and blocked task summaries.")]
    public string ResumeDay(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        date ??= DateTime.Today.ToString("yyyy-MM-dd");

        try
        {
            dailyNoteService.CreateOrLoad(date);

            var all = repository.List();
            var active  = all.Where(t => t.Status is "not-started" or "in-progress").Select(TaskSummary).ToList();
            var urgent  = all.Where(t => t.Urgency is "high" or "urgent").Select(TaskSummary).ToList();
            var blocked = all.Where(t => t.Status == "blocked" || t.Blocked).Select(TaskSummary).ToList();

            return JsonSerializer.Serialize(new
            {
                status = "ready",
                date,
                dailyNotePath = $"daily/{date}.md",
                activeTasks  = active,
                urgentTasks  = urgent,
                blockedTasks = blocked
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            return VaultTools.Error("io_error", ex.Message);
        }
    }

    [McpServerTool(Name = "append_daily_update")]
    [Description("Adds a timestamped update entry directly to today's daily note.")]
    public string AppendDailyUpdate(
        [Description("Text content to add to the daily note")] string text,
        [Description("Related task IDs to link, e.g. [\"TASK-0001\", \"TASK-0002\"]")] string[]? relatedTaskIds = null,
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null)
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        date ??= DateTime.Today.ToString("yyyy-MM-dd");

        try
        {
            dailyNoteService.AppendUpdate(date, text, relatedTaskIds?.ToList());
            return JsonSerializer.Serialize(new { status = "appended", dailyNotePath = $"daily/{date}.md" }, JsonOpts);
        }
        catch (Exception ex)
        {
            return VaultTools.Error("io_error", ex.Message);
        }
    }

    private static object TaskSummary(WorkItemsMcp.Models.TaskItem t) => new
    {
        id = t.Id,
        title = t.Title,
        status = t.Status,
        urgency = t.Urgency,
        blocked = t.Blocked,
        deadline = t.Deadline,
        updated = t.Updated,
        path = $"tasks/{t.Id}.md"
    };
}
