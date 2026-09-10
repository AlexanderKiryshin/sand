using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Opt-in Play Mode integration check, using the disposable preview profile.</summary>
[InitializeOnLoad]
public static class AudioPolishChecks
{
    private const string Key = "Sand.AudioPolishChecks";
    static AudioPolishChecks() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetBool("Sand.CampaignPreview", false);
        SessionState.SetBool("Sand.MechanicsPreview", false);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || UnityEngine.Object.FindObjectOfType<AudioPolishCheckRunner>() != null) return;
        new GameObject("AudioPolishCheckRunner").AddComponent<AudioPolishCheckRunner>();
    }

    public static void Finish(bool passed, string report)
    {
        Directory.CreateDirectory("tasks");
        File.WriteAllText("tasks/audio-polish-validation.md", (passed ? "PASS\n" : "FAIL\n") + report);
        SessionState.SetBool(Key, false);
        SessionState.SetBool("Sand.CampaignPreview", false);
        SessionState.SetBool("Sand.MechanicsPreview", false);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}

public class AudioPolishCheckRunner : MonoBehaviour
{
    private string report = "";
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        IEnumerator checks = Check();
        while (true)
        {
            object current;
            try
            {
                if (!checks.MoveNext()) break;
                current = checks.Current;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AudioPolishChecks.Finish(false, report + exception);
                yield break;
            }
            yield return current;
        }
        AudioPolishChecks.Finish(true, report);
    }

    private IEnumerator Check()
    {
        yield return new WaitForSecondsRealtime(4);
        Time.timeScale = 1;
        AudioListener.pause = false;
        yield return new WaitForSecondsRealtime(2);
        var audio = GameAudio.Instance;
        var menu = FindObjectOfType<AudioSettingsMenu>();
        var conveyor = FindObjectOfType<ConveyorBelt>();
        var view = FindObjectOfType<ConveyorBeltView>();
        Require(audio != null && menu != null && view != null, "Runtime bootstrap creates audio and settings in the existing scene.");
        foreach (string name in new[] { "BucketTap", "BucketFilled", "SandPour", "LevelComplete", "SandGarden" })
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
            Require(clip != null && clip.samples > 100 && clip.loadState == AudioDataLoadState.Loaded, "Imported clip: " + name);
        }
        audio.SendMessage("OnApplicationFocus", true);
        audio.SendMessage("OnApplicationPause", false);
        audio.SetMusicEnabled(true);
        audio.SetSoundsEnabled(true);
        yield return null;
        var music = Field<AudioSource>(audio, "musicSource");
        var effects = Field<AudioSource>(audio, "audioSource");
        var sand = Field<AudioSource>(audio, "sandSource");
        Require(music.loop && sand.loop && !effects.loop && !music.ignoreListenerPause, "Dedicated loop voices respect platform audio pause.");
        LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
        Canvas.ForceUpdateCanvases();
        Press(FindObjectsOfType<Button>().Single(b => b.name == "AudioSettingsButton"));
        Require(menu.IsOpen && Time.timeScale == 0, "Gear button opens a modal and pauses gameplay.");
        Capture("settings-on", 1080, 1920);
        Toggle soundsToggle = Field<Toggle>(menu, "soundsToggle");
        Toggle musicToggle = Field<Toggle>(menu, "musicToggle");
        Press(soundsToggle);
        Require(!audio.SoundsEnabled && audio.MusicEnabled && effects.mute && sand.mute && !music.mute, "Sounds switch silences both effect voices independently of music.");
        Press(musicToggle);
        Require(!audio.MusicEnabled && music.mute && !GameProgressStore.GetBool("sand_audio_music", true), "Music switch applies immediately and persists its value.");
        Capture("settings-off", 864, 1920);
        Press(soundsToggle);
        Require(audio.SoundsEnabled && !audio.MusicEnabled && !effects.mute, "Effects can be enabled while music stays off.");
        Press(Field<RectTransform>(menu, "card").Find("CloseSettings").GetComponent<Button>());
        Require(!menu.IsOpen && Time.timeScale == 1, "Done closes the modal and restores the clock.");
        menu.Open();
        Require(!musicToggle.isOn && soundsToggle.isOn, "Reopening the menu preserves both independent choices.");
        menu.Close();
        audio.SetMusicEnabled(true);
        audio.SendMessage("OnApplicationFocus", false);
        Require(effects.mute && sand.mute && music.mute, "Losing application focus silences all voices.");
        audio.SendMessage("OnApplicationFocus", true);
        Require(!music.mute && !effects.mute, "Returning focus restores the selected audio settings.");

        FindObjectOfType<LevelIntroSandAnimator>().StopIntro();
        var pour = FindObjectOfType<SandPourAnimator>();
        FindObjectOfType<FallingSandSimulator>().SetSimulationPaused(true);
        conveyor.ResetConveyor();
        var fallingBucket = new Bucket(1, 1);
        Require(conveyor.TryPlaceBucket(fallingBucket), "Bucket placed for a real sand collection.");
        var collector = FindObjectOfType<SandCollector>();
        int gridWidth = FindObjectOfType<LevelLoader>().CurrentLevel.gridWidth;
        bool collected = false;
        for (int x = 0; x < gridWidth && !collected; x++) collected = collector.TryCollectGrain(1, x, gridWidth, 1);
        Require(collected && pour.ActiveGrainCount > 0, "Collecting a grain creates an actual sand stream.");
        conveyor.NotifyPourFinished(fallingBucket);
        Require(conveyor.Entries.Count == 1 && FindObjectOfType<BucketCompletionAnimation>() == null, "Full bucket waits for the last visible grain to land.");
        yield return null;
        Require(sand.volume > 0 && sand.isPlaying, "Sand collection starts the rustle voice.");
        yield return new WaitForSeconds(.95f);
        Require(conveyor.Entries.Count == 0, "Landing the last grain completes the bucket.");
        yield return new WaitForSeconds(.65f);
        Require(sand.volume <= .001f && !sand.isPlaying, "The rustle fades out once no sand is falling.");
        Require(music.isPlaying, "Background music keeps looping during gameplay.");

        // Avoid changing the player's stock; create temporary belt buckets in the preview.
        FindObjectOfType<FallingSandSimulator>().SetSimulationPaused(true);
        conveyor.ResetConveyor();
        var bucket = new Bucket(1, 1);
        Require(conveyor.TryPlaceBucket(bucket), "A test bucket is placed on the real belt.");
        bucket.ForceFillToCapacity();
        conveyor.MarkBucketFilledByBooster(bucket);
        conveyor.NotifyPourFinished(bucket);
        var exit = FindObjectOfType<BucketCompletionAnimation>();
        Require(exit != null && conveyor.Entries.Count == 0 && conveyor.FilledBuckets == 1, "A filled bucket frees its slot immediately and retains its visual for the exit.");
        Require(!exit.GetComponent<CanvasGroup>().blocksRaycasts && !exit.GetComponent<Button>().enabled, "Exiting buckets cannot receive clicks.");
        exit.Advance(.28f);
        Require(exit.transform.localScale.x < 1 && exit.GetComponent<CanvasGroup>().alpha < 1, "Exit has intermediate scale and opacity.");
        Capture("bucket-exit", 1080, 1920);
        yield return new WaitForSeconds(.65f);
        Require(FindObjectOfType<BucketCompletionAnimation>() == null, "Finished exit removes its visual.");

        var a = new Bucket(1, 1); var b = new Bucket(2, 1);
        a.LinkedPartner = b; b.LinkedPartner = a;
        Require(conveyor.TryPlaceBucket(a), "Linked pair is placed together.");
        a.ForceFillToCapacity(); conveyor.MarkBucketFilledByBooster(a); conveyor.NotifyPourFinished(a);
        Require(FindObjectOfType<BucketCompletionAnimation>() == null && conveyor.Entries.Count == 2, "A linked bucket waits until its partner is full.");
        b.ForceFillToCapacity(); conveyor.MarkBucketFilledByBooster(b); conveyor.NotifyPourFinished(b);
        Require(FindObjectsOfType<BucketCompletionAnimation>().Length == 2 && conveyor.Entries.Count == 0, "Both filled linked buckets animate out.");
        conveyor.ResetConveyor();
        yield return null;
        Require(FindObjectOfType<BucketCompletionAnimation>() == null, "Reset clears unfinished exit animations.");
        Require(FindObjectsOfType<GameAudio>().Length == 1, "There is one audio manager.");
    }

    private void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        report += "- " + message + "\n";
        Debug.Log("AUDIO_POLISH_PASS: " + message);
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Press(Selectable control)
    {
        var events = EventSystem.current;
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)control.transform;
        var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        events.RaycastAll(pointer, hits);
        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Selectable>() != control)
            throw new InvalidOperationException("UI is blocked: " + control.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void Capture(string name, int width, int height)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        Camera camera = Camera.main;
        RenderMode mode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        var rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        Directory.CreateDirectory("tasks/previews/audio-polish");
        File.WriteAllBytes("tasks/previews/audio-polish/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        canvas.renderMode = mode;
        canvas.worldCamera = previousCamera;
        Destroy(texture); rt.Release(); Destroy(rt);
    }
}
