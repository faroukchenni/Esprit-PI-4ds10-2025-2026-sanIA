/// <summary>
/// LiveSystemsBuilder — FarmTwin Editor Menu
/// ==========================================
/// Adds the following items to the Unity top menu:
///
///   FarmTwin/Build/Setup Live Systems      (one-click: WindSway + AnimalWander check)
///   FarmTwin/Live/Validate Live Systems    (health check — no scene changes)
///   FarmTwin/Live/Toggle Rain              (force rain on/off in Play Mode for testing)
///
/// "Setup Live Systems" does:
///   1. Finds or creates a "LiveSystems" host GameObject
///   2. Adds WindSwaySystem (crop sway) — auto-discovers CyberGrid at runtime
///   3. Verifies AnimalManager (AnimalWander auto-inits per animal at runtime)
///   4. Verifies WeatherSystem + RainController are both present
///   5. Marks the scene dirty so Ctrl+S saves everything
///
/// Drone is intentionally kept as manual DroneController — no auto-patrol.
/// Idempotent: safe to run multiple times — existing components are reused.
/// </summary>

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

#if UNITY_EDITOR
public static class LiveSystemsBuilder
{
    private const string HOST_GO_NAME = "LiveSystems";

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Build / Setup Live Systems
    // ═════════════════════════════════════════════════════════════════════════

    public static void SetupLiveSystems()
    {
        int warnings = 0;

        // ── Step 1: Find or create host GameObject ────────────────────────────
        GameObject host = GameObject.Find(HOST_GO_NAME);
        if (host == null)
        {
            host = new GameObject(HOST_GO_NAME);
            Undo.RegisterCreatedObjectUndo(host, "Create LiveSystems GO");
            Debug.Log($"[LiveSystems] Created '{HOST_GO_NAME}' GameObject.");
        }
        else
        {
            Debug.Log($"[LiveSystems] Reusing existing '{HOST_GO_NAME}' GameObject.");
        }

        // ── Step 2: Add WindSwaySystem ────────────────────────────────────────
        WindSwaySystem existingSway = UnityEngine.Object.FindFirstObjectByType<WindSwaySystem>();
        if (existingSway == null)
        {
            Undo.AddComponent<WindSwaySystem>(host);
            Debug.Log("[LiveSystems] Added WindSwaySystem — crops will sway at runtime.");
        }
        else if (existingSway.gameObject != host)
        {
            Debug.Log($"[LiveSystems] WindSwaySystem already on '{existingSway.gameObject.name}' — skipped.");
        }
        else
        {
            Debug.Log("[LiveSystems] WindSwaySystem already on LiveSystems — OK.");
        }

        // ── Step 3: Verify AnimalManager ─────────────────────────────────────
        // AnimalWander now auto-inits in Start() for ANY animal — even manually placed ones
        AnimalManager animalMgr = UnityEngine.Object.FindFirstObjectByType<AnimalManager>();
        if (animalMgr == null)
        {
            Debug.LogWarning("[LiveSystems] AnimalManager not found in scene. " +
                             "Pre-placed animals will still wander (AnimalWander auto-inits). " +
                             "Add AnimalManager if you need pen-based spawning.");
            warnings++;
        }
        else
        {
            Debug.Log($"[LiveSystems] AnimalManager present on '{animalMgr.gameObject.name}' — OK. " +
                      "AnimalWander auto-inits for all animals (pen-spawned and manually placed).");
        }

        // ── Step 4: Verify WeatherSystem + RainController ─────────────────────
        WeatherSystem  weather = UnityEngine.Object.FindFirstObjectByType<WeatherSystem>();
        RainController rain    = UnityEngine.Object.FindFirstObjectByType<RainController>();

        if (weather == null)
        {
            Debug.LogWarning("[LiveSystems] WeatherSystem not found in scene.");
            warnings++;
        }
        else if (rain == null)
        {
            Debug.LogWarning("[LiveSystems] RainController not found in scene — " +
                             "rain particles won't fire. Add RainController to your Rain GameObject.");
            warnings++;
        }
        else
        {
            Debug.Log("[LiveSystems] WeatherSystem + RainController both present — rain fix active.\n" +
                      "  Tip: use FarmTwin/Live/Toggle Rain to test rain visuals instantly in Play Mode.");
        }

        // ── Step 5: Info — Drone kept as manual ───────────────────────────────
        DroneController droneCtrl = UnityEngine.Object.FindFirstObjectByType<DroneController>();
        if (droneCtrl != null)
            Debug.Log($"[LiveSystems] DroneController on '{droneCtrl.gameObject.name}' — manual navigation (WASD). Autonomous patrol not active by design.");
        else
            Debug.Log("[LiveSystems] No DroneController found — drone manual nav will be inactive.");

        // ── Step 6: Mark scene dirty ──────────────────────────────────────────
        EditorSceneManager.MarkAllScenesDirty();

        // ── Summary ───────────────────────────────────────────────────────────
        if (warnings == 0)
        {
            Debug.Log("[LiveSystems] Setup complete — no warnings.\n" +
                      "  WindSwaySystem  → crops sway automatically at runtime\n" +
                      "  AnimalWander    → all animals wander (auto-init, no manual wiring needed)\n" +
                      "  WeatherSystem   → rain particles wired to RainController\n" +
                      "  Drone           → manual navigation (DroneController)\n" +
                      "Enter Play Mode to see all systems active.");
        }
        else
        {
            Debug.LogWarning($"[LiveSystems] Setup complete with {warnings} warning(s). " +
                             "Check messages above — run 'FarmTwin/Live/Validate Live Systems' anytime.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Live / Toggle Rain  (Play Mode only)
    // ═════════════════════════════════════════════════════════════════════════

    public static void ToggleRain()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LiveSystems] Toggle Rain only works in Play Mode. Enter Play Mode first.");
            return;
        }

        RainController rain = UnityEngine.Object.FindFirstObjectByType<RainController>();
        if (rain == null)
        {
            Debug.LogWarning("[LiveSystems] RainController not found in scene.");
            return;
        }

        rain.Toggle();
        Debug.Log($"[LiveSystems] Rain toggled → now {(rain.isRaining ? "ON ☔" : "OFF ☀")}.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Live / Remove Drone Patrol
    //  Removes DroneRecon from the drone so manual DroneController takes over
    // ═════════════════════════════════════════════════════════════════════════

    public static void RemoveDronePatrol()
    {
        DroneRecon recon = UnityEngine.Object.FindFirstObjectByType<DroneRecon>();
        if (recon == null)
        {
            Debug.Log("[LiveSystems] No DroneRecon found in scene — nothing to remove.");
            return;
        }

        string goName = recon.gameObject.name;
        Undo.DestroyObjectImmediate(recon);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[LiveSystems] Removed DroneRecon from '{goName}'. Manual DroneController navigation active.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Live / Validate Live Systems
    // ═════════════════════════════════════════════════════════════════════════

    public static void ValidateLiveSystems()
    {
        int ok      = 0;
        int missing = 0;

        void Check(bool condition, string label, string hint = "")
        {
            if (condition)
            {
                Debug.Log($"  [OK]      {label}");
                ok++;
            }
            else
            {
                string msg = string.IsNullOrEmpty(hint) ? label : $"{label}  →  {hint}";
                Debug.LogWarning($"  [MISSING] {msg}");
                missing++;
            }
        }

        Debug.Log("=== Live Systems Scene Validation ===");

        Check(UnityEngine.Object.FindFirstObjectByType<WindSwaySystem>() != null,
              "WindSwaySystem",
              "Run FarmTwin/Build/Setup Live Systems");

        Check(UnityEngine.Object.FindFirstObjectByType<AnimalManager>() != null,
              "AnimalManager (optional — AnimalWander auto-inits without it)");

        Check(UnityEngine.Object.FindFirstObjectByType<WeatherSystem>() != null,
              "WeatherSystem");

        Check(UnityEngine.Object.FindFirstObjectByType<RainController>() != null,
              "RainController",
              "Rain particles won't fire without this");

        Check(UnityEngine.Object.FindFirstObjectByType<CyberGrid>() != null,
              "CyberGrid",
              "WindSwaySystem needs CyberGrid to collect crop transforms");

        Check(UnityEngine.Object.FindFirstObjectByType<DroneController>() != null,
              "DroneController (manual navigation)");

        Debug.Log($"=== Result: {ok} OK, {missing} missing ===");

        if (missing == 0)
            Debug.Log("All live systems validated. Enter Play Mode — everything fires automatically.\n" +
                      "Tip: use FarmTwin/Live/Toggle Rain to test rain any time.");
        else
            Debug.LogWarning("Run 'FarmTwin/Build/Setup Live Systems' to fix missing items.");
    }
}
#endif
