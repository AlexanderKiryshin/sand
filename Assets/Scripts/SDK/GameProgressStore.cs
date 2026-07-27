using MirraGames.SDK;
using UnityEngine;

/// <summary>
/// Progress API forwarding to MirraSDK.Data with PlayerPrefs fallback before SDK init.
/// </summary>
public static class GameProgressStore
{
    private static bool UseMirra
    {
        get
        {
            try
            {
                return MirraSDK.IsInitialized;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool HasKey(string key)
    {
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key);
        }

        return PlayerPrefs.HasKey(key);
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key) ? MirraSDK.Data.GetInt(key) : defaultValue;
        }

        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static void SetInt(string key, int value)
    {
        if (UseMirra)
        {
            MirraSDK.Data.SetInt(key, value);
            return;
        }

        PlayerPrefs.SetInt(key, value);
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key) ? MirraSDK.Data.GetFloat(key) : defaultValue;
        }

        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    public static void SetFloat(string key, float value)
    {
        if (UseMirra)
        {
            MirraSDK.Data.SetFloat(key, value);
            return;
        }

        PlayerPrefs.SetFloat(key, value);
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        if (UseMirra)
        {
            return MirraSDK.Data.HasKey(key) ? MirraSDK.Data.GetBool(key) : defaultValue;
        }

        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
    }

    public static void SetBool(string key, bool value)
    {
        if (UseMirra)
        {
            MirraSDK.Data.SetBool(key, value);
            return;
        }

        PlayerPrefs.SetInt(key, value ? 1 : 0);
    }

    public static void Save()
    {
        if (UseMirra)
        {
            MirraSDK.Data.Save();
        }

        PlayerPrefs.Save();
    }
}
