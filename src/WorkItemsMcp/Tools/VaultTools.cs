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
    [Description("Checks if the WorkItemsMCP vault is configured and ready. Call this first if any other WorkItemsMCP tool returns an error, if the user mentions setup issues, or before starting a new session to verify everything is working.")]
    public string GetVaultStatus() =>
        JsonSerializer.Serialize(vaultService.GetStatus(), JsonOpts);

    [McpServerTool(Name = "initialize_vault")]
    [Description("Creates the vault folder structure for WorkItemsMCP at a given path. Call this when get_vault_status returns 'not_initialized' or when the user is setting up the task tracker for the first time. Pass the exact vaultPath from the status response.")]
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
            var vaultPath = vs.GetVaultPath();
            errorJson = JsonSerializer.Serialize(new
            {
                status = "error",
                errorCode = "vault_not_initialized",
                message = $"Vault at '{vaultPath}' is not initialized. Call initialize_vault with path='{vaultPath}'.",
                vaultPath,
                nextStep = $"Call initialize_vault with path=\"{vaultPath}\""
            }, JsonOpts);
            return false;
        }
        errorJson = null;
        return true;
    }
}
