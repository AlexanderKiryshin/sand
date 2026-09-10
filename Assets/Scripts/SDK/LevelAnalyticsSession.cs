using System;
using System.Collections.Generic;

/// <summary>One attempt, one start, and at most one terminal outcome, independent of UI panels.</summary>
public sealed class LevelAnalyticsSession
{
    private readonly Action<string, Dictionary<string, object>> send;
    private readonly Func<double> now;
    private readonly Dictionary<int, int> attempts = new Dictionary<int, int>();
    private Dictionary<string, object> context = new Dictionary<string, object>();
    private double started;
    private bool active;
    private int moves, boosters;
    public bool Active => active;

    public LevelAnalyticsSession(Action<string, Dictionary<string, object>> send, Func<double> now)
    { this.send = send; this.now = now; }

    public void Begin(int zeroBasedLevel, string title, string theme, string mechanics, bool replay)
    {
        End("level_abandoned", "level_changed");
        int level = zeroBasedLevel + 1;
        attempts.TryGetValue(level, out int attempt); attempts[level] = ++attempt;
        context = new Dictionary<string, object>
        {
            ["level"] = level, ["level_id"] = "level_" + level.ToString("000"),
            ["attempt"] = attempt, ["attempt_id"] = Guid.NewGuid().ToString("N"),
            ["title"] = title ?? "", ["theme"] = theme ?? "", ["mechanics"] = mechanics ?? "", ["replay"] = replay
        };
        started = now(); moves = boosters = 0; active = true; Track("level_started");
    }

    public void Track(string name, Dictionary<string, object> values = null)
    {
        var data = new Dictionary<string, object>(context);
        data["duration_seconds"] = Math.Max(0, Math.Round(now() - started, 2));
        data["moves"] = moves; data["boosters"] = boosters;
        if (values != null) foreach (var pair in values) data[pair.Key] = pair.Value;
        send(name, data);
    }

    public void Move() { if (active) moves++; }
    public void Booster() { if (active) boosters++; }
    public void Complete() => End("level_completed", "completed");
    public void Abandon(string reason) => End("level_abandoned", reason);
    public void Restart()
    {
        Track("level_restarted"); End("level_abandoned", "restart");
    }
    public void Fail(string reason) => End("level_failed", reason);
    private void End(string name, string reason)
    {
        if (!active) return;
        active = false; Track(name, new Dictionary<string, object> { ["reason"] = reason });
    }
}
