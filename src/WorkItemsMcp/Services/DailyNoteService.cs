using System.Text;

namespace WorkItemsMcp.Services;

public class DailyNoteService(VaultService vault)
{
    public string CreateOrLoad(string date)
    {
        var paths = vault.GetPaths();
        var filePath = paths.DailyFile(date);

        if (!File.Exists(filePath))
            File.WriteAllText(filePath, GenerateTemplate(date));

        return filePath;
    }

    public void AppendUpdate(string date, string text, List<string>? relatedTaskIds = null)
    {
        var paths = vault.GetPaths();
        var filePath = paths.DailyFile(date);
        if (!File.Exists(filePath)) CreateOrLoad(date);

        var time = DateTime.Now.ToString("HH:mm");
        var sb = new StringBuilder();
        sb.AppendLine($"\n### {time}");
        sb.AppendLine();
        sb.AppendLine(text.TrimEnd());

        if (relatedTaskIds is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Related tasks:");
            foreach (var id in relatedTaskIds)
                sb.AppendLine($"- {id}");
        }

        File.AppendAllText(filePath, sb.ToString());
    }

    public void AppendNewTask(string date, string taskId, string title)
    {
        var paths = vault.GetPaths();
        var filePath = paths.DailyFile(date);
        if (!File.Exists(filePath)) CreateOrLoad(date);

        File.AppendAllText(filePath, $"\n_New task: [{taskId}](../tasks/{taskId}.md) — {title}_\n");
    }

    public void AppendTaskUpdate(string date, string taskId, string? note)
    {
        var paths = vault.GetPaths();
        var filePath = paths.DailyFile(date);
        if (!File.Exists(filePath)) CreateOrLoad(date);

        var text = note ?? $"Task {taskId} updated.";
        File.AppendAllText(filePath, $"\n_Update: [{taskId}](../tasks/{taskId}.md) — {text}_\n");
    }

    private static string GenerateTemplate(string date) =>
        $"""
        # {date}

        ## Daily summary

        ## Updates

        ## New tasks

        ## Updated tasks

        ## Open questions

        ## End-of-day state

        """;
}
