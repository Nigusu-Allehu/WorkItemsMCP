namespace WorkItemsMcp.Services;

public class TaskIdGenerator(VaultService vault)
{
    private readonly object _lock = new();

    public string Next()
    {
        lock (_lock)
        {
            var paths = vault.GetPaths();
            var current = int.Parse(File.ReadAllText(paths.NextIdTxt).Trim());
            var id = $"TASK-{current:D4}";
            File.WriteAllText(paths.NextIdTxt, (current + 1).ToString());
            return id;
        }
    }
}
