using System;
using System.Collections.Generic;
using GameAnalyticsSDK;
using UnityEngine;
using YG;

/// <summary>Web-only fan-out. Each provider has its own pre-initialization queue.</summary>
public sealed class WebGameAnalytics : MonoBehaviour
{
    private sealed class Pending
    {
        public string name;
        public Dictionary<string, object> data;
    }
    private static WebGameAnalytics instance;
    private readonly Queue<Pending> metrica = new Queue<Pending>();
    private readonly Queue<Pending> gameAnalytics = new Queue<Pending>();
    private bool gaReady, metricaReady;
    private float retryAt;
    private const int QueueLimit = 256;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Analytic.ResetSession(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (instance != null) return;
        var root = new GameObject("WebGameAnalytics"); DontDestroyOnLoad(root);
        instance = root.AddComponent<WebGameAnalytics>();
#endif
    }

    public static void Report(string name, Dictionary<string, object> data)
    {
        if (GameProgressStore.IsPreview) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (instance == null) Bootstrap();
        data = new Dictionary<string, object>(data) { ["build"] = Application.version, ["platform"] = "webgl" };
        var pending = new Pending { name = name, data = data };
        instance.Enqueue(instance.metrica, pending); instance.Enqueue(instance.gameAnalytics, pending);
        instance.Flush();
#endif
    }

    private void Enqueue(Queue<Pending> queue, Pending item)
    {
        if (queue.Count >= QueueLimit) queue.Dequeue();
        queue.Enqueue(item);
    }

    private void Update()
    {
        if (Time.unscaledTime < retryAt) return;
        retryAt = Time.unscaledTime + 1;
        try
        {
            if (!metricaReady && YG2.isSDKEnabled && YG2.infoYG.Metrica.enable
                && YG2.infoYG.Metrica.useYandexMetrica && YG2.infoYG.Metrica.metricaCounterID > 0)
                metricaReady = true;
            if (!gaReady)
            {
                var settings = GameAnalytics.SettingsGA;
                int platform = settings != null ? settings.Platforms.IndexOf(RuntimePlatform.WebGLPlayer) : -1;
                if (platform >= 0 && ValidKey(settings.GetGameKey(platform), 32) && ValidKey(settings.GetSecretKey(platform), 40))
                {
                    if (FindObjectOfType<GameAnalytics>() == null) new GameObject("GameAnalytics").AddComponent<GameAnalytics>();
                    if (!GameAnalytics.Initialized) GameAnalytics.Initialize();
                    gaReady = GameAnalytics.Initialized;
                }
            }
        }
        catch (Exception) { Debug.LogWarning("Analytics initialization deferred; verify WebGL analytics settings."); }
        Flush();
    }

    private static bool ValidKey(string key, int length) => !string.IsNullOrWhiteSpace(key) && key.Length == length;

    private void Flush()
    {
        // Failure of one provider cannot suppress the other or interrupt gameplay.
        if (metricaReady) Drain(metrica, p => YG2.MetricaSend(p.name, p.data));
        if (gaReady) Drain(gameAnalytics, SendGameAnalytics);
    }

    private static void Drain(Queue<Pending> queue, Action<Pending> send)
    {
        while (queue.Count > 0)
        {
            try { send(queue.Peek()); queue.Dequeue(); }
            catch (Exception) { break; }
        }
    }

    public static GAProgressionStatus? ProgressionFor(string name)
    {
        if (name == "level_started") return GAProgressionStatus.Start;
        if (name == "level_completed") return GAProgressionStatus.Complete;
        if (name == "level_failed" || name == "level_abandoned") return GAProgressionStatus.Fail;
        return null;
    }

    private static void SendGameAnalytics(Pending pending)
    {
        var fields = new Dictionary<string, object>();
        foreach (var pair in pending.data) fields[pair.Key] = pair.Value is bool flag ? (flag ? 1 : 0) : pair.Value;
        var progression = ProgressionFor(pending.name);
        if (progression.HasValue && pending.data.TryGetValue("level_id", out object id))
            GameAnalytics.NewProgressionEvent(progression.Value, "campaign", id.ToString(), fields);
        else GameAnalytics.NewDesignEvent("gameplay:" + pending.name, fields);
    }

    private void OnApplicationQuit() { Analytic.AbandonLevel("quit"); Flush(); }
}
