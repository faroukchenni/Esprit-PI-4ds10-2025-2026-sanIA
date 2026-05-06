/// <summary>
/// BackendBuilder — FarmTwin Editor Menu
/// =======================================
/// Menu items to launch and configure the SanIA FastAPI backend from inside Unity.
///
///   FarmTwin/Backend/Start Backend Server       — opens a terminal running uvicorn
///   FarmTwin/Backend/Add Digital Twin User      — runs add_digitaltwin_user.py (one-time setup)
///   FarmTwin/Backend/Check Backend Health       — pings /api/v1/health in Play Mode
///   FarmTwin/Backend/Show Backend URL           — logs the URL that Unity will connect to
/// </summary>

using System.IO;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
public static class BackendBuilder
{
    // Path: Unity project = ProjetPi/DigitalTwinUnity, backend = ProjetPi/backend
    private static string BackendPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "../../backend"));

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Backend / Start Backend Server
    // ═════════════════════════════════════════════════════════════════════════

    public static void StartBackend()
    {
        string path   = BackendPath;
        string python = Path.Combine(path, "venv", "Scripts", "python.exe");

        if (!Directory.Exists(path))
        {
            Debug.LogError($"[Backend] Backend folder not found at: {path}");
            return;
        }

        if (!File.Exists(python))
        {
            Debug.LogWarning($"[Backend] venv python not found at {python}. " +
                             "Trying system python...");
            python = "python";
        }

        // Open a new cmd window that stays open so logs are visible
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/k cd /d \"{path}\" && \"{python}\" -m uvicorn app.main:app " +
                              "--reload --host 127.0.0.1 --port 8000",
            UseShellExecute = true,
            CreateNoWindow  = false,
        };

        System.Diagnostics.Process.Start(psi);
        Debug.Log($"[Backend] Launching uvicorn from:\n  {path}\n" +
                  "  Backend will be available at http://localhost:8000\n" +
                  "  Watch the terminal window for startup errors.\n" +
                  "  After it starts, run 'FarmTwin/Backend/Add Digital Twin User' once.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Backend / Add Digital Twin User
    //  Must run ONCE after seed_data.py to register digitaltwin@sania.ai
    // ═════════════════════════════════════════════════════════════════════════

    public static void AddDigitalTwinUser()
    {
        string path   = BackendPath;
        string python = Path.Combine(path, "venv", "Scripts", "python.exe");

        if (!File.Exists(python)) python = "python";

        string script = Path.Combine(path, "add_digitaltwin_user.py");
        if (!File.Exists(script))
        {
            Debug.LogError($"[Backend] Script not found: {script}");
            return;
        }

        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/k cd /d \"{path}\" && \"{python}\" add_digitaltwin_user.py",
            UseShellExecute = true,
            CreateNoWindow  = false,
        };

        System.Diagnostics.Process.Start(psi);
        Debug.Log("[Backend] Running add_digitaltwin_user.py...\n" +
                  "  Registers: digitaltwin@sania.ai / sania2025\n" +
                  "  The terminal window will show [OK] or [ERROR].");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Backend / Show Backend URL
    // ═════════════════════════════════════════════════════════════════════════

    public static void ShowBackendUrl()
    {
        // Try to read from DigitalTwinConfig asset
        DigitalTwinConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<DigitalTwinConfig>(
            "Assets/Resources/DigitalTwinConfig.asset");

        string url = config != null ? config.backendUrl : "http://localhost:8000 (fallback)";
        string email = config != null ? config.loginEmail : "digitaltwin@sania.ai (fallback)";

        Debug.Log($"[Backend] Unity will connect to:\n" +
                  $"  URL:   {url}\n" +
                  $"  Login: {email}\n" +
                  $"  Login endpoint: {url}/api/v1/auth/login\n" +
                  $"  Decision endpoint: {url}/api/v1/irrigation/agent/decision\n\n" +
                  $"Setup checklist:\n" +
                  $"  1. Run FarmTwin/Backend/Start Backend Server\n" +
                  $"  2. Run FarmTwin/Backend/Add Digital Twin User  (once)\n" +
                  $"  3. Enter Play Mode — login fires automatically");
    }
}
#endif
