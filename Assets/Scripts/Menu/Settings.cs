using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Settings window with tabs (Display, Graphics, Controls, Audio). The window is a normal UI prefab:
// Assets/Resources/UI/Settings.prefab, made by Tools > UI > Build Settings Window, then edit it like any prefab.
// At startup (FrameRateLimiter) it is spawned once and kept between scenes. Every button whose name contains
// "Settings" (e.g. the main menu's and the pause menu's) opens it, or call Open() from a button.
// Esc or Back closes it without changes, Apply saves and applies them, Defaults puts everything back on Normal.
public class Settings : MonoBehaviour
{
    const string PrefabPath = "UI/Settings";

    public static Settings Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.isOpen;

    // E.g. the pause menu hides itself while the settings window is open
    public static event Action Opened;
    public static event Action Closed;

    // The Esc that closed the window must not also open / close the pause menu in the same frame
    public static bool BlocksEscape => IsOpen || (Instance != null && Instance.closedFrame == Time.frameCount);

    [Tooltip("The visible part (shade + panel). Hidden while closed.")]
    [SerializeField] GameObject window;
    [Tooltip("Empty = every SettingsTab under the window, in order.")]
    [SerializeField] List<SettingsTab> tabs = new List<SettingsTab>();
    [SerializeField] TMP_Text noteText;
    [SerializeField] Button backButton;
    [SerializeField] Button defaultsButton;
    [SerializeField] Button applyButton;

    [Header("Colors (set at runtime)")]
    [SerializeField] Color textColor = new Color(0.86f, 0.93f, 1f);
    [SerializeField] Color dimText = new Color(0.55f, 0.66f, 0.76f);

    static readonly float[] SensitivityOptions = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f, 3f };

    readonly List<SettingRow> rows = new List<SettingRow>();

    // Values being edited (saved only by Apply)
    List<Vector2Int> resolutions = new List<Vector2Int>();
    int resolutionIndex, displayModeIndex, frameRateIndex;
    bool vSync;
    int preset, renderScale, antiAliasing, shadows, textures, anisotropic;
    float fieldOfView;
    int sensitivityIndex;
    bool invertMouseY;
    bool keepLastKnife;
    float volume;

    bool isOpen;
    bool quitting;
    int currentTab;
    int closedFrame = -1;
    CursorLockMode previousLockState;
    bool previousCursorVisible;

    public static void CreateInstance()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Settings: no prefab at Resources/" + PrefabPath + ". Make it with Tools > UI > Build Settings Window.");
            return;
        }
        GameObject instance = Instantiate(prefab);
        instance.name = prefab.name;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // A copy put into a scene (e.g. under another canvas) is moved to the root: only root objects survive scene loads
        if (transform.parent != null) transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        if (tabs.Count == 0) tabs.AddRange(GetComponentsInChildren<SettingsTab>(true));
        rows.AddRange(GetComponentsInChildren<SettingRow>(true));

        for (int i = 0; i < tabs.Count; i++)
        {
            int tab = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => ShowTab(tab));
        }
        foreach (SettingRow row in rows)
        {
            SettingRow captured = row;
            if (row.previousButton != null) row.previousButton.onClick.AddListener(() => { Step(captured.id, -1); Refresh(); });
            if (row.nextButton != null) row.nextButton.onClick.AddListener(() => { Step(captured.id, 1); Refresh(); });
        }
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (defaultsButton != null) defaultsButton.onClick.AddListener(ResetToDefaults);
        if (applyButton != null) applyButton.onClick.AddListener(ApplyAndClose);

        if (window != null) window.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (!quitting) Debug.LogWarning("Settings: the settings window was destroyed while the game runs. Something deletes it.", this);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isOpen) Close();
        HookButtons();
    }

    void OnApplicationQuit() { quitting = true; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HookButtons();
    }

    // Update is called once per frame
    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // Every button in the scene with "Settings" in its name opens the window
    void HookButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.transform.IsChildOf(transform)) continue;
            if (button.name.IndexOf("settings", StringComparison.OrdinalIgnoreCase) < 0) continue;
            button.onClick.RemoveListener(Open);
            button.onClick.AddListener(Open);
        }
    }

    // ---------------------------------------------------------------- open / close / apply

    public void Open()
    {
        // Called on the prefab asset or a copy (e.g. from a button's On Click list): open the real window instead
        if (this != Instance)
        {
            if (Instance == null) CreateInstance();
            if (Instance != null && Instance != this) Instance.Open();
            return;
        }
        if (isOpen || window == null) return;
        LoadSaved();

        EnsureEventSystem();
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isOpen = true;
        window.SetActive(true);
        ShowTab(currentTab);
        Opened?.Invoke();
    }

    public void Close()
    {
        if (this != Instance)
        {
            if (Instance != null) Instance.Close();
            return;
        }
        if (!isOpen) return;
        isOpen = false;
        closedFrame = Time.frameCount;
        window.SetActive(false);
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        Closed?.Invoke();
    }

    void ShowTab(int tab)
    {
        currentTab = Mathf.Clamp(tab, 0, Mathf.Max(0, tabs.Count - 1));
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = i == currentTab;
            if (tabs[i].page != null) tabs[i].page.SetActive(active);
            if (tabs[i].label != null) tabs[i].label.color = active ? textColor : dimText;
            if (tabs[i].underline != null) tabs[i].underline.enabled = active;
        }
        Refresh();
    }

    void LoadSaved()
    {
        resolutions = GameSettings.AvailableResolutions();
        resolutionIndex = resolutions.IndexOf(GameSettings.Resolution);
        if (resolutionIndex < 0) resolutionIndex = resolutions.Count - 1;
        displayModeIndex = Mathf.Max(0, Array.IndexOf(GameSettings.DisplayModes, GameSettings.DisplayMode));
        vSync = GameSettings.VSync;
        frameRateIndex = Array.IndexOf(GameSettings.FrameRateOptions, GameSettings.FrameRateLimit);
        if (frameRateIndex < 0) frameRateIndex = Array.IndexOf(GameSettings.FrameRateOptions, GameSettings.DefaultFrameRate);

        renderScale = GameSettings.RenderScaleIndex;
        antiAliasing = GameSettings.AntiAliasingIndex;
        shadows = GameSettings.ShadowIndex;
        textures = GameSettings.TextureIndex;
        anisotropic = GameSettings.AnisotropicIndex;
        preset = GameSettings.MatchPreset(renderScale, antiAliasing, shadows, textures, anisotropic);

        fieldOfView = GameSettings.FieldOfView;
        sensitivityIndex = ClosestIndex(SensitivityOptions, GameSettings.MouseSensitivity);
        invertMouseY = GameSettings.InvertMouseY;
        keepLastKnife = GameSettings.KeepLastKnife;
        volume = GameSettings.MasterVolume;
    }

    // Everything on Normal (the resolution and display mode stay)
    void ResetToDefaults()
    {
        vSync = false;
        frameRateIndex = Array.IndexOf(GameSettings.FrameRateOptions, GameSettings.DefaultFrameRate);
        SetPreset(1);
        fieldOfView = GameSettings.DefaultFieldOfView;
        sensitivityIndex = ClosestIndex(SensitivityOptions, GameSettings.DefaultMouseSensitivity);
        invertMouseY = false;
        keepLastKnife = false;
        volume = 1f;
        Refresh();
    }

    void ApplyAndClose()
    {
        if (resolutions.Count > 0) GameSettings.Resolution = resolutions[resolutionIndex];
        GameSettings.DisplayMode = GameSettings.DisplayModes[displayModeIndex];
        GameSettings.VSync = vSync;
        GameSettings.FrameRateLimit = GameSettings.FrameRateOptions[frameRateIndex];

        GameSettings.Preset = preset;
        GameSettings.RenderScaleIndex = renderScale;
        GameSettings.AntiAliasingIndex = antiAliasing;
        GameSettings.ShadowIndex = shadows;
        GameSettings.TextureIndex = textures;
        GameSettings.AnisotropicIndex = anisotropic;

        GameSettings.FieldOfView = fieldOfView;
        GameSettings.MouseSensitivity = SensitivityOptions[sensitivityIndex];
        GameSettings.InvertMouseY = invertMouseY;
        GameSettings.KeepLastKnife = keepLastKnife;
        GameSettings.MasterVolume = volume;

        GameSettings.ApplyAll();
        Close();
    }

    void SetPreset(int value)
    {
        preset = value;
        renderScale = GameSettings.PresetValue(value, 0);
        antiAliasing = GameSettings.PresetValue(value, 1);
        shadows = GameSettings.PresetValue(value, 2);
        textures = GameSettings.PresetValue(value, 3);
        anisotropic = GameSettings.PresetValue(value, 4);
    }

    // ---------------------------------------------------------------- rows

    void Step(SettingId id, int step)
    {
        switch (id)
        {
            case SettingId.Resolution: resolutionIndex = Cycle(resolutionIndex, step, resolutions.Count); break;
            case SettingId.DisplayMode: displayModeIndex = Cycle(displayModeIndex, step, GameSettings.DisplayModes.Length); break;
            case SettingId.VSync: vSync = !vSync; break;
            case SettingId.FrameRate: frameRateIndex = Cycle(frameRateIndex, step, GameSettings.FrameRateOptions.Length); break;

            case SettingId.Preset: SetPreset(Cycle(preset == GameSettings.CustomPreset ? 1 : preset, step, 4)); break;
            case SettingId.RenderScale: renderScale = Cycle(renderScale, step, GameSettings.RenderScaleOptions.Length); GraphicsChanged(); break;
            case SettingId.AntiAliasing: antiAliasing = Cycle(antiAliasing, step, GameSettings.AntiAliasingOptions.Length); GraphicsChanged(); break;
            case SettingId.Shadows: shadows = Cycle(shadows, step, GameSettings.ShadowDistanceOptions.Length); GraphicsChanged(); break;
            case SettingId.Textures: textures = Cycle(textures, step, GameSettings.TextureMipLimitOptions.Length); GraphicsChanged(); break;
            case SettingId.Anisotropic: anisotropic = Cycle(anisotropic, step, GameSettings.AnisotropicOptions.Length); GraphicsChanged(); break;

            case SettingId.MouseSensitivity: sensitivityIndex = Mathf.Clamp(sensitivityIndex + step, 0, SensitivityOptions.Length - 1); break;
            case SettingId.InvertMouseY: invertMouseY = !invertMouseY; break;
            case SettingId.FieldOfView: fieldOfView = Mathf.Clamp(fieldOfView + step * 5f, GameSettings.MinFieldOfView, GameSettings.MaxFieldOfView); break;
            case SettingId.KeepLastKnife: keepLastKnife = !keepLastKnife; break;

            case SettingId.MasterVolume: volume = Mathf.Clamp01(Mathf.Round((volume + step * 0.1f) * 10f) / 10f); break;
        }
    }

    string ValueText(SettingId id)
    {
        switch (id)
        {
            case SettingId.Resolution:
                Vector2Int size = resolutions.Count > 0 ? resolutions[resolutionIndex] : new Vector2Int(Screen.width, Screen.height);
                return size.x + " x " + size.y;
            case SettingId.DisplayMode: return GameSettings.DisplayModeNames[displayModeIndex];
            case SettingId.VSync: return vSync ? "On" : "Off";
            case SettingId.FrameRate:
                int rate = GameSettings.FrameRateOptions[frameRateIndex];
                return rate > 0 ? rate + " FPS" : "Unlimited";

            case SettingId.Preset: return GameSettings.PresetNames[preset];
            case SettingId.RenderScale: return Mathf.RoundToInt(GameSettings.RenderScaleOptions[renderScale] * 100f) + "%";
            case SettingId.AntiAliasing: return GameSettings.AntiAliasingNames[antiAliasing];
            case SettingId.Shadows: return GameSettings.ShadowNames[shadows];
            case SettingId.Textures: return GameSettings.TextureNames[textures];
            case SettingId.Anisotropic: return GameSettings.AnisotropicNames[anisotropic];

            case SettingId.MouseSensitivity: return SensitivityOptions[sensitivityIndex].ToString("0.00") + "x";
            case SettingId.InvertMouseY: return invertMouseY ? "On" : "Off";
            case SettingId.FieldOfView: return Mathf.RoundToInt(fieldOfView).ToString();
            case SettingId.KeepLastKnife: return keepLastKnife ? "On" : "Off";

            case SettingId.MasterVolume: return Mathf.RoundToInt(volume * 100f) + "%";
        }
        return "";
    }

    // A single graphics option changed by hand: the preset becomes the matching one or Custom
    void GraphicsChanged()
    {
        preset = GameSettings.MatchPreset(renderScale, antiAliasing, shadows, textures, anisotropic);
    }

    void Refresh()
    {
        foreach (SettingRow row in rows)
        {
            if (row.valueText == null) continue;
            row.valueText.text = ValueText(row.id);
            row.valueText.color = row.id == SettingId.FrameRate && vSync ? dimText : textColor;
        }

        if (noteText == null) return;
        SettingsTab tab = currentTab < tabs.Count ? tabs[currentTab] : null;
        bool hasFrameRate = tab != null && tab.page != null && HasRow(tab.page, SettingId.FrameRate);
        if (hasFrameRate && vSync)
            noteText.text = "VSync is on: the game follows the monitor's refresh rate. Menus always run at " + GameSettings.MenuFrameRate + " FPS.";
        else
            noteText.text = tab != null ? tab.note : "";
    }

    bool HasRow(GameObject page, SettingId id)
    {
        foreach (SettingRow row in rows)
            if (row.id == id && row.transform.IsChildOf(page.transform)) return true;
        return false;
    }

    static int Cycle(int index, int step, int count) => count <= 0 ? 0 : (index + step + count) % count;

    static int ClosestIndex(float[] options, float value)
    {
        int best = 0;
        for (int i = 1; i < options.Length; i++)
            if (Mathf.Abs(options[i] - value) < Mathf.Abs(options[best] - value)) best = i;
        return best;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }
}
