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
    [Description("ALWAYS call this at the start of a work session or when the user asks 'what am I working on today', 'what's my day look like', 'catch me up', 'what were my tasks', 'resume my work', 'what did I have going on', or 'start my day'. Creates today's daily note if needed and returns all active, urgent, and blocked work items so the agent can give an immediate summary.")]
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
    [Description("Logs a summary of the current conversation or user update into today's daily note. Use after any meaningful exchange about tasks — e.g. when the user shares news, gives an update on progress, or at the end of a work session to record what happened today.")]
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
