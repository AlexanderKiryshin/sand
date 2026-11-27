// Standalone C# regression harness, following BUSES/Tests/AudioRegression.
// Compile with YOUTUBE_PLAYABLES and the real LocalizationManager, GameSdkReadiness,
// YoutubeAudio, YoutubeGameplayFreeze, CommonAudio, PauseChangeEvent and UnityEngineAudio sources.
// Only engine/SDK boundaries are stubbed; do not copy this file into Assets.
using System;
using System.Collections;
using System.Reflection;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using MirraGames.SDK.UnityEngine;
using UnityEngine;
using YTGameSDK;

public static class YoutubeBusesBehaviorChecks
{
    static int failures;
    public static int Main()
    {
        foreach (LanguageType language in Enum.GetValues(typeof(LanguageType)))
            Run("YouTube SDK language selection: " + language, () =>
            {
                Application.systemLanguage = language == LanguageType.German ? SystemLanguage.Russian : SystemLanguage.German;
                YoutubePlatform.Ready = false;
                MirraSDK.Language.Current = language;
                MirraSDK.Language.Reads = 0;
                var localization = new LocalizationManager();
                typeof(LocalizationManager).GetMethod("InitializeLocalizations", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(localization, null);
                int changes = 0;
                localization.onLanguageChange += () => changes++;
                var routine = (IEnumerator)typeof(LocalizationManager)
                    .GetMethod("SetLanguageRoutine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(localization, null);
                Equal(true, routine.MoveNext());
                var wait = (WaitUntil)routine.Current;
                Equal(false, wait.Predicate());
                Equal(LanguageType.English, localization.GetCurrentLanguage());
                Equal(0, MirraSDK.Language.Reads);
                Equal(0, changes);
                YoutubePlatform.Ready = true;
                Equal(true, wait.Predicate());
                Equal(false, routine.MoveNext());
                var expected = language == LanguageType.French ? LanguageType.English : language;
                Equal(expected, localization.GetCurrentLanguage());
                Equal(1, MirraSDK.Language.Reads);
                Equal(1, changes);
            });
        Run("YouTube callbacks update Mirra volume and the cached enabled state", () =>
        {
            YTGameWrapper.Instance.ChangeAudio(false);
            Equal(false, YoutubeAudio.IsYtAudioEnabled); Equal(0f, AudioListener.volume);
            YTGameWrapper.Instance.ChangeAudio(true);
            Equal(true, YoutubeAudio.IsYtAudioEnabled); Equal(1f, AudioListener.volume);
        });
        Run("Mute survives Mirra pause/resume", () =>
        {
            YTGameWrapper.Instance.ChangeAudio(false);
            PauseMirra(true); PauseMirra(false);
            Equal(0f, AudioListener.volume);
        });
        Run("Unmute during Mirra pause is audible only after resume", () =>
        {
            YTGameWrapper.Instance.ChangeAudio(false); PauseMirra(true);
            YTGameWrapper.Instance.ChangeAudio(true); Equal(0f, AudioListener.volume);
            PauseMirra(false); Equal(1f, AudioListener.volume);
        });
        Run("Resume rechecks the current wrapper audio setting, like BUSES", () =>
        {
            Time.timeScale = 2f;
            YoutubeGameplayFreeze.PauseFromSdk();
            YTGameWrapper.Instance.AudioEnabled = false; // Setting changes without delivering a callback.
            YoutubeGameplayFreeze.ResumeFromSdk();
            Equal(false, YoutubeAudio.IsYtAudioEnabled); Equal(0f, AudioListener.volume);
            Equal(false, AudioListener.pause); Equal(2f, Time.timeScale);
        });
        Run("Enabling audio does not remove an SDK pause", () =>
        {
            YTGameWrapper.Instance.ChangeAudio(false);
            YoutubeGameplayFreeze.PauseFromSdk(); YTGameWrapper.Instance.ChangeAudio(true);
            Equal(true, AudioListener.pause); Equal(0f, Time.timeScale);
            YoutubeGameplayFreeze.ResumeFromSdk();
            Equal(false, AudioListener.pause); Equal(1f, AudioListener.volume);
        });
        Run("Finishing an ad keeps an active SDK pause and mute", () =>
        {
            YoutubeGameplayFreeze.PauseFromSdk(); YoutubeGameplayFreeze.SetAdPending(true);
            YTGameWrapper.Instance.ChangeAudio(false); YoutubeGameplayFreeze.SetAdPending(false);
            Equal(true, AudioListener.pause); Equal(true, YoutubeGameplayFreeze.PlatformPaused);
            YoutubeGameplayFreeze.ResumeFromSdk();
            Equal(false, AudioListener.pause); Equal(0f, AudioListener.volume);
            PauseMirra(true); PauseMirra(false); Equal(0f, AudioListener.volume);
        });
        Run("SDK resume waits for the pending ad before restoring audio", () =>
        {
            YoutubeGameplayFreeze.SetAdPending(true); YoutubeGameplayFreeze.PauseFromSdk();
            YoutubeGameplayFreeze.ResumeFromSdk(); Equal(true, AudioListener.pause);
            YTGameWrapper.Instance.AudioEnabled = false;
            YoutubeGameplayFreeze.SetAdPending(false);
            Equal(false, AudioListener.pause); Equal(0f, AudioListener.volume);
        });
        Run("Missing wrapper preserves the last applied mute", () =>
        {
            YoutubeAudio.Apply(false); YoutubeGameplayFreeze.PauseFromSdk();
            YTGameWrapper.Instance = null; YoutubeGameplayFreeze.ResumeFromSdk();
            Equal(0f, AudioListener.volume); Equal(false, AudioListener.pause);
        });
        return failures == 0 ? 0 : 1;
    }
    static void Run(string name, Action test)
    {
        AudioListener.volume = 1f; AudioListener.pause = false;
        MirraSDK.Audio = new UnityEngineAudio(new UnityEngineAudio_Configuration(), new Events());
        YTGameWrapper.Instance = new YTGameWrapper { AudioEnabled = true };
        YoutubeGameplayFreeze.SetAdPending(false); YoutubeGameplayFreeze.ResumeFromSdk();
        Time.timeScale = 1f; YoutubeAudio.Apply(true);
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    static void PauseMirra(bool paused) => ((CommonAudio)MirraSDK.Audio).OnEvent(new PauseChangeEvent(paused));
    static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }
}

public static class YoutubePlatform
{
    public static bool IsPlayer => true;
    public static bool Ready;
}

namespace UnityEngine
{
    public class Object
    {
        public static T FindObjectOfType<T>() where T : class => YTGameWrapper.Instance as T;
        public static void Destroy(Object value) { }
    }
    public class GameObject : Object { }
    public class MonoBehaviour : Object
    {
        protected GameObject gameObject = new GameObject();
        protected void StartCoroutine(IEnumerator routine) { }
    }
    public enum SystemLanguage { English, Russian, Turkish, German, French, Japanese, Unknown }
    public static class Application { public static SystemLanguage systemLanguage; }
    public static class AudioListener { public static float volume = 1f; public static bool pause; }
    public static class Time { public static float timeScale = 1f; }
    public class WaitUntil
    {
        public readonly Func<bool> Predicate;
        public WaitUntil(Func<bool> predicate) { Predicate = predicate; }
    }
}
namespace MirraGames.SDK.Common
{
    public enum LanguageType { English, Russian, Turkish, German, French }
    public class WrapperAttribute : Attribute { }
    public class ProviderAttribute : Attribute { public ProviderAttribute(Type type) { } }
    public interface IAudio { float Volume { get; set; } bool Pause { get; set; } }
    public interface IEventListener<T> { void OnEvent(T value); }
    public interface IEventAggregator { void Subscribe<T>(IEventListener<T> listener); }
    public class Events : IEventAggregator { public void Subscribe<T>(IEventListener<T> listener) { } }
    public static class Logger
    {
        public static void CreateError(object source, Exception error) { throw error; }
        public static void CreateError(object source, string method, Exception error) { throw error; }
    }
}
namespace MirraGames.SDK.UnityEngine
{
    public class UnityEngineAudio_Configuration { public bool HandlePauseEvents = true; }
}
namespace MirraGames.SDK
{
    public static class MirraSDK
    {
        public static IAudio Audio;
        public static bool IsInitialized => true;
        public static readonly LanguageBoundary Language = new LanguageBoundary();
        public static readonly TimeBoundary Time = new TimeBoundary();
    }
    public class LanguageBoundary
    {
        private LanguageType current;
        public int Reads;
        public LanguageType Current
        {
            get { Reads++; return current; }
            set => current = value;
        }
    }
    public class TimeBoundary
    {
        public float Scale { get => global::UnityEngine.Time.timeScale; set => global::UnityEngine.Time.timeScale = value; }
    }
}
namespace YTGameSDK
{
    public class YTGameWrapper
    {
        public static YTGameWrapper Instance;
        public bool AudioEnabled;
        public bool IsYTGameAudioEnabled() => AudioEnabled;
        public void ChangeAudio(bool enabled) { AudioEnabled = enabled; YoutubeAudio.Apply(enabled); }
    }
}
