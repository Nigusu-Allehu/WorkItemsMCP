namespace WorkItemsMcp.Models;

public class TaskItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "not-started";
    public string Urgency { get; set; } = "normal";
    public string Created { get; set; } = "";
    public string Updated { get; set; } = "";
    public string? Deadline { get; set; }
    public bool Blocked { get; set; }
    public string Description { get; set; } = "";
    public List<string> NextActions { get; set; } = [];
    public List<string> OpenQuestions { get; set; } = [];
}
