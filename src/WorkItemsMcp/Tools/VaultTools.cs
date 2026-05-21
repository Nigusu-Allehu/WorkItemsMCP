using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WorkItemsMcp.Services;

namespace WorkItemsMcp.Tools;

[McpServerToolType]
public class VaultTools(VaultService vaultService)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [McpServerTool(Name = "get_vault_status")]
    [Description("Checks whether the TASK_TRACKER_VAULT environment variable is set and the vault is initialized.")]
    public string GetVaultStatus() =>
        JsonSerializer.Serialize(vaultService.GetStatus(), JsonOpts);

    [McpServerTool(Name = "initialize_vault")]
    [Description("Creates the vault folder structure at the given path. Sets up .task-tracker/, tasks/, daily/, and views/.")]
    public string InitializeVault(
        [Description("Absolute path where the vault should be created, e.g. /home/user/task-vault")] string path)
    {
        try
        {
            vaultService.Initialize(path);

            // Check whether the env var is already pointing at this path
            var configured = vaultService.IsConfigured();
            var currentPath = vaultService.GetVaultPath();
            var envAligned = configured && string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase);

            return JsonSerializer.Serialize(new
            {
                status = "initialized",
                vaultPath = path,
                envVarSet = envAligned,
                nextStep = envAligned
                    ? null
                    : $"Set the environment variable {VaultService.EnvVar}={path} in your MCP client config and restart the client before using other tools."
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            return Error("io_error", ex.Message);
        }
    }

    internal static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    internal static bool CheckVault(VaultService vs, out string? errorJson)
    {
        if (!vs.IsConfigured())
        {
            errorJson = JsonSerializer.Serialize(new
            {
                status = "setup_required",
                errorCode = "vault_not_configured",
                message = "TASK_TRACKER_VAULT is not configured.",
                requiredEnvVar = VaultService.EnvVar
            }, JsonOpts);
            return false;
        }
        if (!vs.IsInitialized())
        {
            errorJson = Error("vault_not_initialized", "Vault is not initialized. Call initialize_vault first.");
            return false;
        }
        errorJson = null;
        return true;
    }
}
