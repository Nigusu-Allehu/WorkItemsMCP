using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WorkItemsMcp.Services;

namespace WorkItemsMcp.Tools;

[McpServerToolType]
public class ViewTools(VaultService vaultService, ViewBuilder viewBuilder)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [McpServerTool(Name = "rebuild_views")]
    [Description("Regenerates the markdown summary views (active, urgent, blocked, completed work items). Call this after any batch of task updates, or when the user asks to refresh their task views or see an up-to-date summary dashboard.")]
    public string RebuildViews()
    {
        if (!VaultTools.CheckVault(vaultService, out var err)) return err!;

        try
        {
            var views = viewBuilder.Rebuild();
            return JsonSerializer.Serialize(new { status = "rebuilt", views }, JsonOpts);
        }
        catch (Exception ex)
        {
            return VaultTools.Error("io_error", ex.Message);
        }
    }
}
