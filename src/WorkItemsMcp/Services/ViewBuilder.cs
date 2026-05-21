using System.Text;
using WorkItemsMcp.Models;

namespace WorkItemsMcp.Services;

public class ViewBuilder(VaultService vault, TaskRepository repository)
{
    public List<string> Rebuild()
    {
        var tasks = repository.List();
        var paths = vault.GetPaths();

        WriteView(paths.ViewFile("active.md"),    "Active Tasks",    tasks.Where(t => t.Status is "not-started" or "in-progress").ToList());
        WriteView(paths.ViewFile("urgent.md"),    "Urgent Tasks",    tasks.Where(t => t.Urgency is "high" or "urgent").ToList());
        WriteView(paths.ViewFile("blocked.md"),   "Blocked Tasks",   tasks.Where(t => t.Status == "blocked" || t.Blocked).ToList());
        WriteView(paths.ViewFile("completed.md"), "Completed Tasks", tasks.Where(t => t.Status is "done" or "cancelled").ToList());

        return ["views/active.md", "views/urgent.md", "views/blocked.md", "views/completed.md"];
    }

    private static void WriteView(string path, string title, List<TaskItem> tasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();

        if (tasks.Count == 0)
        {
            sb.AppendLine("No tasks.");
        }
        else
        {
            foreach (var task in tasks)
            {
                sb.AppendLine($"## [{task.Id}](../tasks/{task.Id}.md) — {task.Title}");
                sb.AppendLine();
                sb.AppendLine($"- Status: {MarkdownTaskSerializer.FormatStatus(task.Status)}");
                sb.AppendLine($"- Urgency: {MarkdownTaskSerializer.FormatUrgency(task.Urgency)}");
                sb.AppendLine($"- Blocked: {(task.Blocked ? "Yes" : "No")}");
                if (!string.IsNullOrEmpty(task.Deadline))
                    sb.AppendLine($"- Deadline: {task.Deadline}");
                sb.AppendLine($"- Updated: {task.Updated}");
                sb.AppendLine();
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }
}
