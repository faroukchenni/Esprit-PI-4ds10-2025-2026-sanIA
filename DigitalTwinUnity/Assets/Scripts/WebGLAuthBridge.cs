using UnityEngine;

/// <summary>
/// Receives the logged-in user's JWT from the website when the Digital Twin
/// runs as a WebGL build embedded in the page.
///
/// Website usage (after unityInstance is ready):
///   unityInstance.SendMessage('WebGLAuthBridge', 'ReceiveJwt', userJwtToken);
///
/// Self-bootstraps at scene load — no manual scene placement needed.
/// In the Unity editor the website never calls this, so SanIAApiClient falls
/// back to logging in with the credentials in DigitalTwinConfig.asset.
/// </summary>
public class WebGLAuthBridge : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<WebGLAuthBridge>() == null)
        {
            GameObject go = new GameObject("WebGLAuthBridge");
            go.AddComponent<WebGLAuthBridge>();
            Debug.Log("[WebGLAuthBridge] Auto-created.");
        }
    }

    void Awake()
    {
        if (FindObjectsByType<WebGLAuthBridge>(FindObjectsSortMode.None).Length > 1)
        { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Called by the website via SendMessage.</summary>
    public void ReceiveJwt(string jwt)
    {
        if (SanIAApiClient.Instance != null)
            SanIAApiClient.Instance.SetJwtFromWeb(jwt);
        else
            // SanIAApiClient may not be ready yet — store and apply on next frame
            StartCoroutine(ApplyWhenReady(jwt));
    }

    System.Collections.IEnumerator ApplyWhenReady(string jwt)
    {
        while (SanIAApiClient.Instance == null)
            yield return null;
        SanIAApiClient.Instance.SetJwtFromWeb(jwt);
    }
}
