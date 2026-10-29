using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Audio preferences remain available from the game, with independent switches.</summary>
public class AudioSettingsMenu : MonoBehaviour
{
    private GameAudio audio;
    private Canvas canvas;
    private RectTransform openButton, overlay, card;
    private TextMeshProUGUI title, soundsLabel, musicLabel, closeLabel;
    private Toggle soundsToggle, musicToggle;
    private Image soundsTrack, musicTrack;
    private RectTransform soundsKnob, musicKnob;
    private TextMeshProUGUI soundsState, musicState;
    private LocalizationManager localization;
    private float previousTimeScale;
#if UNITY_EDITOR
    private int cheatTaps;
    private float lastCheatTap;
#endif
    public bool IsOpen => overlay != null && overlay.gameObject.activeSelf;

    private void Start()
    {
        audio = GameAudio.Instance;
        canvas = FindObjectOfType<Canvas>();
        if (audio == null || canvas == null) return;
        BuildMenu();
        audio.SettingsChanged += Refresh;
        localization = LocalizationManager.Instance;
        if (localization != null) localization.onLanguageChange += Refresh;
        Refresh();
        ApplyLayout();
    }

    private void Update()
    {
        if (openButton == null) return;
        ApplyLayout();
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.U)
            && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            UnlockAllLevels();
#endif
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void BuildMenu()
    {
        openButton = Panel("AudioSettingsButton", canvas.transform, new Vector2(72, 72), new Color(.15f, .37f, .37f));
        openButton.anchorMin = openButton.anchorMax = openButton.pivot = new Vector2(0, 1);
        openButton.gameObject.AddComponent<Button>().onClick.AddListener(Open);
        Image icon = Panel("SettingsIcon", openButton, new Vector2(44, 44), Color.white).GetComponent<Image>();
        icon.sprite = GameUiSymbols.Get(GameUiSymbol.Settings);
        icon.type = Image.Type.Simple;
        icon.raycastTarget = false;

        overlay = Panel("AudioSettingsMenu", canvas.transform, Vector2.zero, new Color(.04f, .12f, .15f, .72f));
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().sprite = null;
        overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        card = Panel("Card", overlay, new Vector2(620, 530), new Color(.97f, .98f, .93f));
        // An inner surface consumes clicks so changing a switch never closes the menu.
        card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        title = Label("Title", card, new Vector2(0, 192), new Vector2(540, 70), 43);
#if UNITY_EDITOR
        title.raycastTarget = true;
        Button cheatTrigger = title.gameObject.AddComponent<Button>();
        cheatTrigger.targetGraphic = title;
        cheatTrigger.transition = Selectable.Transition.None;
        cheatTrigger.onClick.AddListener(TapCheatTitle);
#endif
        soundsToggle = BuildSwitch("Sounds", 65, out soundsLabel, out soundsTrack, out soundsKnob, out soundsState);
        musicToggle = BuildSwitch("Music", -53, out musicLabel, out musicTrack, out musicKnob, out musicState);
        soundsToggle.onValueChanged.AddListener(value => { audio.SetSoundsEnabled(value); audio.PlayTap(); });
        musicToggle.onValueChanged.AddListener(value => { audio.SetMusicEnabled(value); audio.PlayTap(); });
        RectTransform close = Panel("CloseSettings", card, new Vector2(510, 76), new Color(.15f, .37f, .37f));
        close.anchoredPosition = new Vector2(0, -180);
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        closeLabel = Label("Label", close, Vector2.zero, new Vector2(490, 70), 30);
        closeLabel.color = new Color(1, .99f, .91f);
        overlay.gameObject.SetActive(false);
    }

    private Toggle BuildSwitch(string name, float y, out TextMeshProUGUI label, out Image track,
        out RectTransform knob, out TextMeshProUGUI state)
    {
        RectTransform row = Panel(name, card, new Vector2(530, 100), new Color(.89f, .94f, .90f));
        row.anchoredPosition = new Vector2(0, y);
        label = Label("Label", row, new Vector2(-94, 0), new Vector2(280, 82), 32);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform switchRect = Panel("Switch", row, new Vector2(170, 62), Color.white);
        switchRect.anchoredPosition = new Vector2(159, 0);
        track = switchRect.GetComponent<Image>();
        track.raycastTarget = false;
        knob = Panel("Knob", switchRect, new Vector2(46, 46), new Color(1, .99f, .93f));
        knob.GetComponent<Image>().sprite = HintUiSprites.CircleBadge;
        knob.GetComponent<Image>().raycastTarget = false;
        state = Label("State", switchRect, Vector2.zero, new Vector2(100, 54), 23);
        state.color = Color.white;
        Toggle toggle = row.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = row.GetComponent<Image>();
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        return toggle;
    }

    private void ApplyLayout()
    {
        Rect safe = Screen.safeArea;
        float factor = Mathf.Max(.001f, canvas.scaleFactor);
        openButton.anchoredPosition = new Vector2(safe.xMin / factor + 16, -(Screen.height - safe.yMax) / factor - 104);
        float scale = Mathf.Min(1f, (safe.width / factor - 40) / 620, (safe.height / factor - 40) / 530);
        card.localScale = Vector3.one * Mathf.Max(.1f, scale);
    }

    public void Open()
    {
        if (overlay == null || IsOpen) return;
        audio.PlayTap();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        overlay.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        GameIsReady.SetWindowOpen(this, true);
#if UNITY_EDITOR
        cheatTaps = 0;
#endif
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        overlay.gameObject.SetActive(false);
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = previousTimeScale;
        GameIsReady.SetWindowOpen(this, false);
        if (audio != null) audio.PlayTap();
    }

    private void Refresh()
    {
        if (audio == null || title == null) return;
        title.text = CampaignText.Get(LevelCheats.AllLevelsUnlocked ? "cheat_levels_unlocked" : "audio_settings");
        soundsLabel.text = CampaignText.Get("audio_sounds");
        musicLabel.text = CampaignText.Get("audio_music");
        closeLabel.text = CampaignText.Get("audio_done");
        RefreshSwitch(soundsToggle, soundsTrack, soundsKnob, soundsState, audio.SoundsEnabled);
        RefreshSwitch(musicToggle, musicTrack, musicKnob, musicState, audio.MusicEnabled);
    }

#if UNITY_EDITOR
    private void TapCheatTitle()
    {
        if (Time.unscaledTime - lastCheatTap > 1f) cheatTaps = 0;
        lastCheatTap = Time.unscaledTime;
        if (++cheatTaps < 5) return;
        cheatTaps = 0;
        UnlockAllLevels();
    }

    private void UnlockAllLevels()
    {
        LevelCheats.UnlockAllLevels();
        FindObjectOfType<CampaignGallery>()?.RefreshProgress();
        Refresh();
        audio.PlayTap();
        Debug.Log("Level cheat: all main and winter levels unlocked.");
    }

#endif

    private static void RefreshSwitch(Toggle toggle, Image track, RectTransform knob, TextMeshProUGUI state, bool on)
    {
        toggle.SetIsOnWithoutNotify(on);
        track.color = on ? new Color(.16f, .49f, .40f) : new Color(.43f, .49f, .48f);
        knob.anchoredPosition = new Vector2(on ? 53 : -53, 0);
        state.rectTransform.anchoredPosition = new Vector2(on ? -25 : 25, 0);
        state.text = CampaignText.Get(on ? "audio_on" : "audio_off");
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = HintUiSprites.RoundedPanel;
        image.type = Image.Type.Sliced;
        image.color = color;
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * .7f;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(.13f, .29f, .29f);
        text.raycastTarget = false;
        return text;
    }

    private void OnDisable() => Close();
    private void OnDestroy()
    {
        if (audio != null) audio.SettingsChanged -= Refresh;
        if (localization != null) localization.onLanguageChange -= Refresh;
        if (openButton != null) Destroy(openButton.gameObject);
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
