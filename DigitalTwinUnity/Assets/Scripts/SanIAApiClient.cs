/// <summary>
/// SanIAApiClient — Unity Backend HTTP Client
/// ============================================
/// Singleton MonoBehaviour that handles all communication with the SanIA
/// FastAPI backend.
///
/// Authentication flow:
///   1. On Start → auto-login with configured credentials
///   2. JWT token is stored in memory and attached to every request
///   3. IsReady = true once login succeeds; IrrigationDecisionManager
///      checks this before sending requests
///
/// Endpoints used:
///   POST /api/v1/auth/login           (OAuth2 form-encoded, returns JWT)
///   POST /api/v1/irrigation/agent/decision  (JSON body, auth required)
///
/// Uses UnityWebRequest coroutines — compatible with Unity 6 URP.
/// No external dependencies: JsonUtility is used for serialization.
///
/// IMPORTANT: UnityWebRequest is NOT subject to browser CORS rules.
/// The backend CORS middleware allows localhost origins for web clients —
/// Unity's native HTTP bypasses CORS entirely.
/// </summary>

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ── JSON Request / Response DTOs ─────────────────────────────────────────────

/// <summary>
/// Mirrors the FastAPI AgentSensorReading Pydantic model.
/// Field names are snake_case to match JSON keys directly.
/// </summary>
[System.Serializable]
public class IrrigationRequest
{
    public string field_id;
    public string soil_type;
    public int    crop_age_days;
    public float  temperature_C;
    public float  humidity_pct;
    public float  soil_moisture_pct;
    public float  field_capacity_pct;
    public float  wilting_point_pct;
    public float  area_m2;
    public float  root_zone_depth_m;
    public float  application_efficiency_pct;
    /// <summary>Set to digital_twin for Unity; IoT builds use iot_maquette (matches backend).</summary>
    public string decision_source = "digital_twin";
    /// <summary>mm precip for ML rain_mm + rain guard; &lt; 0 = let backend use Open-Meteo.</summary>
    public float twin_rain_mm_24h = -1f;
    /// <summary>ET0 mm/day from twin weather; &lt; 0 = let backend use Open-Meteo.</summary>
    public float twin_et0_mm = -1f;
}

/// <summary>
/// Mirrors AgentDecisionResponse — only the fields used by the Digital Twin.
/// JsonUtility silently ignores unknown fields (lag_features_used, weather_context).
/// </summary>
[System.Serializable]
public class IrrigationDecisionResult
{
    public bool   irrigate;
    public float  confidence;
    public float  volume_m3;
    public string decision_label;
    public string reason;
    public string model_version;
}

[System.Serializable]
class TokenResponse
{
    public string access_token;
    public string token_type;
}

/// <summary>
/// Mirrors IrrigationStatusResponse from the backend /status endpoint.
/// zones is populated by JsonUtility — each element is an IrrigationStatusZone.
/// </summary>
[System.Serializable]
public class IrrigationStatusZone
{
    public string field_id;
    public string decision_label;
    public float  confidence;
    public bool   irrigate;
    public float  volume_m3;
}

[System.Serializable]
class IrrigationStatusResponse
{
    public IrrigationStatusZone[] zones;
    public string model_version;
}

// ── Client ────────────────────────────────────────────────────────────────────

public class SanIAApiClient : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────

    public static SanIAApiClient Instance { get; private set; }

    // ── State ──────────────────────────────────────────────────────────────────

    /// <summary>True once the backend login has succeeded and the JWT is stored.</summary>
    public bool IsReady => !string.IsNullOrEmpty(_jwt);

    private string _jwt = "";

    /// <summary>
    /// Called by the website via SendMessage when the user is already logged in.
    /// Skips the credential login entirely and uses the website's JWT directly.
    /// </summary>
    public void SetJwtFromWeb(string jwt)
    {
        if (string.IsNullOrEmpty(jwt)) return;
        _jwt = jwt;
        Debug.Log("[SanIAApiClient] JWT received from website — skipping login.");
    }
    private string _backendUrl = "http://localhost:8000";

    // ── API prefix matching main.py include_router prefixes ───────────────────
    private const string LOGIN_PATH    = "/api/v1/auth/login";
    private const string DECISION_PATH = "/api/v1/irrigation/agent/decision";
    private const string STATUS_PATH   = "/api/v1/irrigation/status";

    // ── Retry settings ─────────────────────────────────────────────────────────
    [Tooltip("Seconds to wait before retrying a failed login")]
    public float loginRetryDelaySec = 15f;

    [Tooltip("Maximum number of login attempts before giving up")]
    public int maxLoginAttempts = 5;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Called by IrrigationDecisionManager.Initialize() — NOT from Awake/Start
    /// so that the config is passed in before login begins.
    /// </summary>
    public void Initialize(string backendUrl, string email, string password)
    {
        _backendUrl = backendUrl.TrimEnd('/');
        StartCoroutine(LoginWithRetry(email, password));
    }

    // ── Login ──────────────────────────────────────────────────────────────────

    private IEnumerator LoginWithRetry(string email, string password)
    {
        for (int attempt = 1; attempt <= maxLoginAttempts; attempt++)
        {
            TwinEventLogger.Log("BACKEND", $"Login attempt {attempt}/{maxLoginAttempts}...", "info");

            bool success = false;
            yield return StartCoroutine(LoginCoroutine(email, password, ok => success = ok));

            if (success)
            {
                TwinEventLogger.Log("BACKEND", "Authentication OK — SanIA agent ready.", "info");
                yield break;
            }

            if (attempt < maxLoginAttempts)
            {
                TwinEventLogger.Log("BACKEND",
                    $"Login failed. Retry in {loginRetryDelaySec}s...", "warn");
                yield return new WaitForSeconds(loginRetryDelaySec);
            }
        }

        TwinEventLogger.Log("BACKEND",
            "Login failed after all attempts. Irrigation decisions will be skipped.", "warn");
    }

    private IEnumerator LoginCoroutine(string email, string password, Action<bool> onDone)
    {
        // OAuth2PasswordRequestForm: application/x-www-form-urlencoded (not multipart WWWForm).
        string body =
            "grant_type=password" +
            "&username=" + Uri.EscapeDataString(email) +
            "&password=" + Uri.EscapeDataString(password);

        string url = _backendUrl + LOGIN_PATH;
        Debug.Log($"[SanIAApiClient] Attempting login → POST {url}");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string hint = req.responseCode == 404
                ? $"\n  [HINT] 404 means backend is running but endpoint not found OR a different " +
                  $"server is on port 8000.\n  Run FarmTwin/Backend/Start Backend Server to launch " +
                  $"the correct backend."
                : req.responseCode == 401
                ? "\n  [HINT] 401 means wrong credentials or user not registered.\n  " +
                  "Run FarmTwin/Backend/Add Digital Twin User to create the account."
                : req.result == UnityWebRequest.Result.ConnectionError
                ? "\n  [HINT] Backend is not running.\n  " +
                  "Run FarmTwin/Backend/Start Backend Server to launch it."
                : "";

            Debug.LogWarning($"[SanIAApiClient] Login HTTP error {req.responseCode} — {req.error}{hint}");
            onDone?.Invoke(false);
            yield break;
        }

        try
        {
            TokenResponse token = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
            if (!string.IsNullOrEmpty(token.access_token))
            {
                _jwt = token.access_token;
                onDone?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("[SanIAApiClient] Login response missing access_token.");
                onDone?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SanIAApiClient] Login response parse error: {ex.Message}");
            onDone?.Invoke(false);
        }
    }

    // ── Decision Request ───────────────────────────────────────────────────────

    /// <summary>
    /// Sends a sensor reading to the irrigation agent and returns the decision.
    ///
    /// Callback signature: (result, errorMessage)
    ///   result      — non-null if successful
    ///   errorMessage — non-null if request failed
    /// </summary>
    public void RequestDecision(
        IrrigationRequest payload,
        Action<IrrigationDecisionResult, string> onResult)
    {
        if (!IsReady)
        {
            onResult?.Invoke(null, "Not authenticated — login pending.");
            return;
        }
        StartCoroutine(DecisionCoroutine(payload, onResult));
    }

    // ── Status Polling ────────────────────────────────────────────────────────

    /// <summary>
    /// Polls GET /api/v1/irrigation/status for the latest decision
    /// per field without pushing a new sensor reading.
    /// Useful for refreshing the Digital Twin display when the Pi pushes
    /// decisions independently.
    ///
    /// Callback: (zones[], errorMessage)
    /// </summary>
    public void PollStatus(Action<IrrigationStatusZone[], string> onResult)
    {
        if (!IsReady)
        {
            onResult?.Invoke(null, "Not authenticated — login pending.");
            return;
        }
        StartCoroutine(StatusCoroutine(onResult));
    }

    private IEnumerator StatusCoroutine(Action<IrrigationStatusZone[], string> onResult)
    {
        string url = _backendUrl + STATUS_PATH;

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", "Bearer " + _jwt);
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = $"HTTP {req.responseCode}: {req.error}";
            if (req.responseCode == 401)
            {
                _jwt = "";
                err = "Token expired — re-login required.";
            }
            onResult?.Invoke(null, err);
            yield break;
        }

        try
        {
            IrrigationStatusResponse resp =
                JsonUtility.FromJson<IrrigationStatusResponse>(req.downloadHandler.text);
            onResult?.Invoke(resp?.zones, null);
        }
        catch (Exception ex)
        {
            onResult?.Invoke(null, $"Parse error: {ex.Message}");
        }
    }

    private IEnumerator DecisionCoroutine(
        IrrigationRequest payload,
        Action<IrrigationDecisionResult, string> onResult)
    {
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        string url = _backendUrl + DECISION_PATH;

        // Always log request so we can verify fields sent to the backend
        Debug.Log($"[SanIAApiClient] POST {url}\nREQUEST: {json}");

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + _jwt);
        req.timeout = 15;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = $"HTTP {req.responseCode}: {req.error}";

            if (req.responseCode == 401)
            {
                _jwt = "";
                err = "Token expired — re-login required.";
            }

            Debug.LogWarning($"[SanIAApiClient] Decision failed: {err}");
            onResult?.Invoke(null, err);
            yield break;
        }

        string responseText = req.downloadHandler.text;
        // Always log raw response so we can see exactly what the model returns
        Debug.Log($"[SanIAApiClient] RESPONSE ({payload.field_id}): {responseText}");

        try
        {
            IrrigationDecisionResult result =
                JsonUtility.FromJson<IrrigationDecisionResult>(responseText);

            if (result == null || string.IsNullOrEmpty(result.decision_label))
            {
                onResult?.Invoke(null, $"Invalid response body: {responseText}");
            }
            else
            {
                onResult?.Invoke(result, null);
            }
        }
        catch (Exception ex)
        {
            onResult?.Invoke(null, $"Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generic authenticated GET — returns raw JSON string via callback.
    /// callback(json, errorMessage): errorMessage is null on success.
    /// </summary>
    public void GetJson(string path, Action<string, string> callback)
    {
        if (!IsReady) { callback?.Invoke(null, "Not authenticated"); return; }
        StartCoroutine(GetJsonCoroutine(path, callback));
    }

    private IEnumerator GetJsonCoroutine(string path, Action<string, string> callback)
    {
        using UnityWebRequest req = UnityWebRequest.Get(_backendUrl + path);
        req.SetRequestHeader("Authorization", "Bearer " + _jwt);
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (req.responseCode == 401) _jwt = "";
            callback?.Invoke(null, $"HTTP {req.responseCode}: {req.error}");
            yield break;
        }
        callback?.Invoke(req.downloadHandler.text, null);
    }

    /// <summary>
    /// Generic authenticated POST with JSON body — returns raw JSON via callback.
    /// callback(json, errorMessage): errorMessage is null on success.
    /// Timeout is 30s — suitable for RAG/LLM endpoints that can be slow.
    /// </summary>
    public void PostJson(string path, string jsonBody, Action<string, string> callback)
    {
        if (!IsReady) { callback?.Invoke(null, "Not authenticated"); return; }
        StartCoroutine(PostJsonCoroutine(path, jsonBody, callback));
    }

    private IEnumerator PostJsonCoroutine(string path, string jsonBody, Action<string, string> callback)
    {
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest req = new UnityWebRequest(_backendUrl + path, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + _jwt);
        req.timeout = 30;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (req.responseCode == 401) _jwt = "";
            callback?.Invoke(null, $"HTTP {req.responseCode}: {req.error}");
            yield break;
        }
        callback?.Invoke(req.downloadHandler.text, null);
    }
}
