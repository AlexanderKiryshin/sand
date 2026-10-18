using MirraGames.SDK;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

/// <summary>The C# SDK instance can exist before its asynchronous JS providers are usable.</summary>
public static class GameSdkReadiness
{
    internal static Func<bool> CheckBridgeReady = ReadBridgeReady;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int sand_sdk_isBridgeReady();
#endif

    public static bool IsReady
    {
        get
        {
            try
            {
                return MirraSDK.IsInitialized && CheckBridgeReady();
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool ReadBridgeReady()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return sand_sdk_isBridgeReady() != 0;
#else
        return true;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        CheckBridgeReady = ReadBridgeReady;
    }
}
