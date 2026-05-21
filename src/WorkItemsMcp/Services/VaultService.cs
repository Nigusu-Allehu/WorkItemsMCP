using System.Text.Json;

namespace WorkItemsMcp.Services;

public class VaultService
{
    public const string EnvVar = "TASK_TRACKER_VAULT";

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public string? GetVaultPath() => Environment.GetEnvironmentVariable(EnvVar);

    public bool IsConfigured() => !string.IsNullOrEmpty(GetVaultPath());

    public bool IsInitialized()
    {
        var path = GetVaultPath();
        if (string.IsNullOrEmpty(path)) return false;
        return Directory.Exists(Path.Combine(path, ".task-tracker"))
            && File.Exists(Path.Combine(path, ".task-tracker", "index.json"));
    }

    public VaultPaths GetPaths() => new(GetVaultPath()!);

    public object GetStatus()
    {
        var path = GetVaultPath();
        if (string.IsNullOrEmpty(path))
        {
            return new
            {
                status = "setup_required",
                configured = false,
                initialized = false,
                message = "TASK_TRACKER_VAULT is not configured.",
                requiredEnvVar = EnvVar
            };
        }
        if (!IsInitialized())
        {
            return new
            {
                status = "not_initialized",
                configured = true,
                initialized = false,
                vaultPath = path
            };
        }
        return new
        {
            status = "ready",
            configured = true,
            initialized = true,
            vaultPath = path
        };
    }

    public object Initialize(string path)
    {
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".task-tracker"));
        Directory.CreateDirectory(Path.Combine(path, "tasks"));
        Directory.CreateDirectory(Path.Combine(path, "daily"));
        Directory.CreateDirectory(Path.Combine(path, "views"));

        var trackerDir = Path.Combine(path, ".task-tracker");

        WriteIfMissing(Path.Combine(trackerDir, "index.json"), "{}");
        WriteIfMissing(Path.Combine(trackerDir, "next-id.txt"), "1");
        WriteIfMissing(
            Path.Combine(trackerDir, "config.json"),
            JsonSerializer.Serialize(new { version = "1.0", vaultPath = path }, JsonOpts));

        foreach (var view in new[] { "active.md", "urgent.md", "blocked.md", "completed.md" })
        {
            var viewPath = Path.Combine(path, "views", view);
            WriteIfMissing(viewPath, $"# {Path.GetFileNameWithoutExtension(view)}\n\nNo tasks.\n");
        }

        return new { status = "initialized", vaultPath = path };
    }

    private static void WriteIfMissing(string path, string content)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, content);
    }
}

public class VaultPaths
{
    public string Root { get; }
    public string Tracker { get; }
    public string Tasks { get; }
    public string Daily { get; }
    public string Views { get; }
    public string IndexJson { get; }
    public string NextIdTxt { get; }

    public VaultPaths(string root)
    {
        Root = root;
        Tracker = Path.Combine(root, ".task-tracker");
        Tasks = Path.Combine(root, "tasks");
        Daily = Path.Combine(root, "daily");
        Views = Path.Combine(root, "views");
        IndexJson = Path.Combine(Tracker, "index.json");
        NextIdTxt = Path.Combine(Tracker, "next-id.txt");
    }

    public string TaskFile(string taskId) => Path.Combine(Tasks, $"{taskId}.md");
    public string DailyFile(string date) => Path.Combine(Daily, $"{date}.md");
    public string ViewFile(string name) => Path.Combine(Views, name);
}
