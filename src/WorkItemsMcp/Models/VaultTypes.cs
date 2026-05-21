namespace WorkItemsMcp.Models;

public class TaskSummary
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Urgency { get; set; } = "";
    public bool Blocked { get; set; }
    public string? Deadline { get; set; }
    public string Updated { get; set; } = "";
    public string Path { get; set; } = "";
}

public class SearchMatch
{
    public string TaskId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string Snippet { get; set; } = "";
}

public class CreateTaskInput
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "not-started";
    public string Urgency { get; set; } = "normal";
    public string? Deadline { get; set; }
    public bool Blocked { get; set; }
    public List<string> NextActions { get; set; } = [];
    public List<string> OpenQuestions { get; set; } = [];
    public string Date { get; set; } = "";
}
