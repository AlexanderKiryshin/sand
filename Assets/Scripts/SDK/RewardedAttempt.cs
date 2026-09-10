using System;

/// <summary>One confirmed SDK outcome can settle one reward request exactly once.</summary>
public sealed class RewardedAttempt
{
    private readonly Action success;
    private readonly Action failure;
    public bool IsSettled { get; private set; }
    public RewardedAttempt(Action success, Action failure) { this.success = success; this.failure = failure; }
    public bool Complete(bool watched)
    {
        if (IsSettled) return false;
        IsSettled = true;
        if (watched) success?.Invoke(); else failure?.Invoke();
        return true;
    }
}
