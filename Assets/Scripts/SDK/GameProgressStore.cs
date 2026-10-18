using MirraGames.SDK;
using UnityEngine;

/// <summary>
/// Progress API forwarding to MirraSDK.Data with PlayerPrefs fallback before SDK init.
/// </summary>
public static class GameProgressStore
{
    // Opt-in editor captures use a disposable in-memory profile.
    public static bool IsPreview
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool("Sand.CampaignPreview", false)
                || UnityEditor.SessionState.GetBool("Sand.MarketingPreview", false)
                || UnityEditor.SessionState.GetBool("Sand.ReworkPreview", false)
                || UnityEditor.SessionState.GetBool("Sand.AudioPolishChecks", false)
                || UnityEditor.SessionState.GetBool("Sand.BuildSizeChecks", false);
#else
            return false;
#endif
        }
    }
#if UNITY_EDITOR
    private static readonly System.Collections.Generic.Dictionary<string, object> preview = new System.Collections.Generic.Dictionary<string, object>();
    private static T PreviewGet<T>(string key, T fallback) => preview.TryGetValue(key, out object value) && value is T typed ? typed : fallback;
#endif
    private static bool UseMirra
    {
        get
        {
            try
            {
                return GameSdkReadiness.IsReady;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool HasKey(string key)
    {
#if UNITY_EDITOR
        if (IsPreview) return preview.ContainsKey(key);
#endif
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key);
        }

        return PlayerPrefs.HasKey(key);
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
#if UNITY_EDITOR
        if (IsPreview) return PreviewGet(key, defaultValue);
#endif
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key) ? MirraSDK.Data.GetInt(key) : defaultValue;
        }

        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static void SetInt(string key, int value)
    {
#if UNITY_EDITOR
        if (IsPreview) { preview[key] = value; return; }
#endif
        if (UseMirra)
        {
            MirraSDK.Data.SetInt(key, value);
            return;
        }

        PlayerPrefs.SetInt(key, value);
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
#if UNITY_EDITOR
        if (IsPreview) return PreviewGet(key, defaultValue);
#endif
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key) ? MirraSDK.Data.GetFloat(key) : defaultValue;
        }

        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    public static void SetFloat(string key, float value)
    {
#if UNITY_EDITOR
        if (IsPreview) { preview[key] = value; return; }
#endif
        if (UseMirra)
        {
            MirraSDK.Data.SetFloat(key, value);
            return;
        }

        PlayerPrefs.SetFloat(key, value);
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
#if UNITY_EDITOR
        if (IsPreview) return PreviewGet(key, defaultValue);
#endif
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key) ? MirraSDK.Data.GetBool(key) : defaultValue;
        }

        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
    }

    public static void SetBool(string key, bool value)
    {
#if UNITY_EDITOR
        if (IsPreview) { preview[key] = value; return; }
#endif
        if (UseMirra)
        {
            MirraSDK.Data.SetBool(key, value);
            return;
        }

        PlayerPrefs.SetInt(key, value ? 1 : 0);
    }

    public static void Save()
    {
        if (IsPreview) return;
        if (UseMirra)
        {
            MirraSDK.Data.Save();
        }

        PlayerPrefs.Save();
    }
}
