// Test-only timing scopes. Installed by prepare_sand_performance.py in the disposable project.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SandPerformanceProbe
{
    public sealed class Metric
    {
        public readonly double[] milliseconds = new double[4096];
        public readonly int[] calls = new int[4096];
    }
    static readonly Dictionary<string, Metric> metrics = new Dictionary<string, Metric>();
    static int firstFrame;
    static bool enabled;
    public static void Begin() { metrics.Clear(); firstFrame = Time.frameCount; enabled = true; }
    public static Scope Sample(string label)
    {
        if (!enabled) return default;
        if (!metrics.TryGetValue(label, out var metric)) { metric = new Metric(); metrics.Add(label, metric); }
        return new Scope(metric, Time.frameCount - firstFrame);
    }
    public struct Scope : IDisposable
    {
        readonly Metric metric;
        readonly int frame;
        readonly long start;
        public Scope(Metric metric, int frame) { this.metric = metric; this.frame = frame; start = Stopwatch.GetTimestamp(); }
        public void Dispose()
        {
            if (metric == null || frame >= metric.calls.Length) return;
            metric.milliseconds[frame] += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            metric.calls[frame]++;
        }
    }
    public static double End(string phase, string path)
    {
        enabled = false;
        int frames = Math.Max(1, Time.frameCount - firstFrame + 1);
        var total = new double[frames];
        var lines = new List<string>();
        foreach (var pair in metrics.OrderBy(pair => pair.Key))
        {
            var times = pair.Value.milliseconds.Take(frames).OrderBy(value => value).ToArray();
            double p95 = times[(int)((times.Length - 1) * .95)];
            int calls = pair.Value.calls.Take(frames).Sum();
            int maxCalls = pair.Value.calls.Take(frames).Max();
            if (phase.StartsWith("intro-") && pair.Key == "SandRenderer.TextureUpload" && maxCalls > 1
                && Environment.GetEnvironmentVariable("SAND_PERF_BASELINE_RUN") != "1")
                throw new InvalidOperationException("Intro uploaded the same texture more than once per frame.");
            lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5:F4},{6:F4},{7:F4}", phase, pair.Key, frames, calls, maxCalls, times.Average(), p95, times.Last()));
            if (pair.Key.EndsWith(".Update") || pair.Key == "SandRenderer.RefreshDirty")
                for (int i = 0; i < frames; i++) total[i] += pair.Value.milliseconds[i];
        }
        File.AppendAllLines(path, lines);
        Array.Sort(total);
        return total[(int)((total.Length - 1) * .95)];
    }
}
