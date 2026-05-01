using UnityEngine;
using System;

public static class TwinEventLogger
{
    // Allows other scripts (like FuturisticUI) to listen for logs.
    public static event Action<string, string, string> OnLogAdded;

    public static void Log(string source, string message, string type = "info")
    {
        // Log to Unity Console for debugging
        string debugPrefix = $"[{source}]";
        if (type == "warning") Debug.LogWarning($"{debugPrefix} {message}");
        else if (type == "error") Debug.LogError($"{debugPrefix} {message}");
        else Debug.Log($"{debugPrefix} {message}");

        // Broadcast to UI
        OnLogAdded?.Invoke(source, message, type);
    }
}
