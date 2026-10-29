using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using YTGameSDK;

/// <summary>BUSES-style C# progress adapter; only an acknowledged write clears dirty state.</summary>
public sealed class YoutubeCloudProgress
{
    [Serializable] public sealed class Entry { public string key; public string value; }
    [Serializable] public sealed class SaveBlob { public int version = 2; public Entry[] entries; }
    private readonly Dictionary<string, string> values = new Dictionary<string, string>();
    private int revision, savedRevision, failures;
    private bool saving;
    private float nextSave;
    public bool Loaded { get; private set; }
    public bool Writable { get; private set; }
    public bool Dirty => revision != savedRevision;
    public Action<string> Status;

    public void CompleteLoad(string normalizedJson)
    {
        if (Loaded) return; // A timeout or an earlier completion is terminal for this session.
        Loaded = true;
        try
        {
            if (normalizedJson == null) throw new FormatException();
            var blob = JsonUtility.FromJson<SaveBlob>(normalizedJson);
            if (blob == null || blob.version != 2 || blob.entries == null) throw new FormatException();
            var loaded = new Dictionary<string, string>();
            foreach (var e in blob.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.key) || e.value == null || loaded.ContainsKey(e.key))
                    throw new FormatException();
                loaded.Add(e.key, e.value);
            }
            foreach (var pair in loaded) values.Add(pair.Key, pair.Value);
            Writable = true;
        }
        catch { Writable = false; Status?.Invoke("Progress could not be loaded. This session will not be saved."); }
    }
    public bool HasKey(string key) => values.ContainsKey(key);
    public double Get(string key, double fallback)
    {
        if (!values.TryGetValue(key, out string text)) return fallback;
        if (bool.TryParse(text, out bool flag)) return flag ? 1 : 0;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : fallback;
    }
    public void Set(string key, double value)
    {
        if (!Loaded || double.IsNaN(value) || double.IsInfinity(value)) return;
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        if (values.TryGetValue(key, out string previous) && previous == text) return;
        values[key] = text; revision++; failures = 0;
        nextSave = Time.realtimeSinceStartup + .25f;
    }
    public void RequestSave() { failures = 0; nextSave = Time.realtimeSinceStartup; }
    public void Tick(YTGameWrapper wrapper, bool paused)
    {
        if (!paused && Time.realtimeSinceStartup >= nextSave) Flush(wrapper.SendGameSaveData);
    }
    public void Flush(Action<string, Action<string>> send, bool exiting = false)
    {
        if (!Writable || saving || !Dirty || failures >= 3) return;
        var entries = new Entry[values.Count]; int i = 0;
        foreach (var pair in values) entries[i++] = new Entry { key = pair.Key, value = pair.Value };
        string json = JsonUtility.ToJson(new SaveBlob { entries = entries });
        int bytes = Encoding.UTF8.GetByteCount(json);
        if (Math.Max(bytes, json.Length * 2) >= 3 * 1024 * 1024)
        { Writable = false; Status?.Invoke("Progress is too large to save."); return; }
        if (exiting && bytes >= 64 * 1024) return;
        int writingRevision = revision;
        saving = true;
        bool settled = false;
        send(json, result => {
            if (settled) return;
            settled = true; saving = false;
            if (result == "success") { savedRevision = writingRevision; failures = 0; Status?.Invoke(""); }
            else
            {
                failures++;
                if (result == "timeout") Writable = false; // Do not race an unresolved server write.
                Status?.Invoke("Progress has not been saved. Please keep the game open and try again.");
            }
            nextSave = Time.realtimeSinceStartup + (failures > 0 ? failures * 2 : 0);
        });
    }
}
