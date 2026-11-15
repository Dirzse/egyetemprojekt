using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The player's settings (saved in PlayerPrefs) and applying them. Everything starts on "Normal".
// The menu scenes always run at a fixed 60 FPS, every other scene uses the FPS limit chosen in the settings.
// Graphics changes go to a runtime copy of the URP asset, so the project's own assets are never modified.
public static class GameSettings
{
    public static readonly string[] MenuScenes = { "MainMenu", "CharacterSelect" };
    public const int MenuFrameRate = 60;
    public const int DefaultFrameRate = 60;

    // ---------------------------------------------------------------- options (index 1 or the marked one is "Normal")

    // 0 = unlimited
    public static readonly int[] FrameRateOptions = { 30, 60, 75, 120, 144, 165, 240, 0 };

    public static readonly FullScreenMode[] DisplayModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    public static readonly string[] DisplayModeNames = { "Fullscreen", "Borderless", "Windowed" };

    public static readonly string[] PresetNames = { "Low", "Normal", "High", "Ultra", "Custom" };
    public const int CustomPreset = 4;

    public static readonly float[] RenderScaleOptions = { 0.5f, 0.67f, 0.75f, 0.85f, 1f, 1.25f };
    public static readonly int[] AntiAliasingOptions = { 1, 2, 4, 8 };   // MSAA samples, 1 = off
    public static readonly string[] AntiAliasingNames = { "Off", "2x MSAA", "4x MSAA", "8x MSAA" };
    public static readonly float[] ShadowDistanceOptions = { 0f, 25f, 50f, 100f, 150f };
    public static readonly string[] ShadowNames = { "Off", "Low", "Normal", "High", "Ultra" };
    public static readonly int[] TextureMipLimitOptions = { 2, 1, 0 };   // 0 = full resolution
    public static readonly string[] TextureNames = { "Low", "Normal", "High" };
    public static readonly AnisotropicFiltering[] AnisotropicOptions =
        { AnisotropicFiltering.Disable, AnisotropicFiltering.Enable, AnisotropicFiltering.ForceEnable };
    public static readonly string[] AnisotropicNames = { "Off", "On", "Forced" };

    // Preset table: render scale, anti-aliasing, shadows, textures, anisotropic (option indexes)
    static readonly int[,] Presets =
    {
        { 2, 0, 1, 1, 0 },   // Low
        { 4, 1, 2, 2, 1 },   // Normal
        { 4, 2, 3, 2, 1 },   // High
        { 5, 3, 4, 2, 2 },   // Ultra
    };

    public const float DefaultFieldOfView = 70f;
    public const float MinFieldOfView = 55f;
    public const float MaxFieldOfView = 100f;
    public const float DefaultMouseSensitivity = 1f;

    // ---------------------------------------------------------------- saved values

    const string Prefix = "Settings.";

    public static int FrameRateLimit { get => GetInt("FrameRate", DefaultFrameRate); set => SetInt("FrameRate", Mathf.Max(0, value)); }
    public static bool VSync { get => GetInt("VSync", 0) == 1; set => SetInt("VSync", value ? 1 : 0); }
    public static FullScreenMode DisplayMode { get => (FullScreenMode)GetInt("DisplayMode", (int)Screen.fullScreenMode); set => SetInt("DisplayMode", (int)value); }

    public static Vector2Int Resolution
    {
        get => new Vector2Int(GetInt("Width", Screen.width), GetInt("Height", Screen.height));
        set { SetInt("Width", value.x); SetInt("Height", value.y); }
    }

    public static int Preset { get => GetInt("Preset", 1); set => SetInt("Preset", value); }
    public static int RenderScaleIndex { get => GetIndex("RenderScale", RenderScaleOptions.Length, Presets[1, 0]); set => SetInt("RenderScale", value); }
    public static int AntiAliasingIndex { get => GetIndex("AntiAliasing", AntiAliasingOptions.Length, Presets[1, 1]); set => SetInt("AntiAliasing", value); }
    public static int ShadowIndex { get => GetIndex("Shadows", ShadowDistanceOptions.Length, Presets[1, 2]); set => SetInt("Shadows", value); }
    public static int TextureIndex { get => GetIndex("Textures", TextureMipLimitOptions.Length, Presets[1, 3]); set => SetInt("Textures", value); }
    public static int AnisotropicIndex { get => GetIndex("Anisotropic", AnisotropicOptions.Length, Presets[1, 4]); set => SetInt("Anisotropic", value); }

    public static float FieldOfView { get => PlayerPrefs.GetFloat(Prefix + "FieldOfView", DefaultFieldOfView); set => PlayerPrefs.SetFloat(Prefix + "FieldOfView", Mathf.Clamp(value, MinFieldOfView, MaxFieldOfView)); }
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat(Prefix + "MouseSensitivity", DefaultMouseSensitivity); set => PlayerPrefs.SetFloat(Prefix + "MouseSensitivity", Mathf.Max(0.05f, value)); }
    public static bool InvertMouseY { get => GetInt("InvertMouseY", 0) == 1; set => SetInt("InvertMouseY", value ? 1 : 0); }
    public static bool KeepLastKnife { get => GetInt("KeepLastKnife", 0) == 1; set => SetInt("KeepLastKnife", value ? 1 : 0); }
    public static float MasterVolume { get => PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f); set => PlayerPrefs.SetFloat(Prefix + "MasterVolume", Mathf.Clamp01(value)); }

    static int GetInt(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
    static void SetInt(string key, int value) => PlayerPrefs.SetInt(Prefix + key, value);
    static int GetIndex(string key, int count, int fallback) => Mathf.Clamp(GetInt(key, fallback), 0, count - 1);

    // Option indexes of a preset (0-3): render scale, anti-aliasing, shadows, textures, anisotropic
    public static int PresetValue(int preset, int setting) => Presets[Mathf.Clamp(preset, 0, 3), setting];

    // Which preset these options match, or Custom
    public static int MatchPreset(int renderScale, int antiAliasing, int shadows, int textures, int anisotropic)
    {
        int[] values = { renderScale, antiAliasing, shadows, textures, anisotropic };
        for (int p = 0; p < 4; p++)
        {
            bool same = true;
            for (int s = 0; s < values.Length; s++) same &= Presets[p, s] == values[s];
            if (same) return p;
        }
        return CustomPreset;
    }

    // ---------------------------------------------------------------- applying

    static bool initialized;
    static RenderPipelineAsset originalPipeline;
    static UniversalRenderPipelineAsset runtimePipeline;
    static int originalMipLimit;
    static AnisotropicFiltering originalAnisotropic;

    public static bool IsMenuScene(string sceneName) => Array.IndexOf(MenuScenes, sceneName) >= 0;

    // Every resolution of the monitor once, smallest first
    public static List<Vector2Int> AvailableResolutions()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (UnityEngine.Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!list.Contains(size)) list.Add(size);
        }
        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        if (!list.Contains(current)) list.Add(current);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    // Called once before the first scene loads (FrameRateLimiter)
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        originalPipeline = QualitySettings.renderPipeline;
        originalMipLimit = QualitySettings.globalTextureMipmapLimit;
        originalAnisotropic = QualitySettings.anisotropicFiltering;

        // A copy of the URP asset gets the graphics options, the asset in the project stays untouched
        RenderPipelineAsset current = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
        UniversalRenderPipelineAsset source = current as UniversalRenderPipelineAsset;
        if (source != null)
        {
            runtimePipeline = Object.Instantiate(source);
            runtimePipeline.name = source.name + " (Settings)";
            QualitySettings.renderPipeline = runtimePipeline;
        }
        Application.quitting += RestoreProjectSettings;

        if (PlayerPrefs.HasKey(Prefix + "Width"))
        {
            Vector2Int size = Resolution;
            Screen.SetResolution(size.x, size.y, DisplayMode);
        }
        ApplyGraphics();
        AudioListener.volume = MasterVolume;

        SceneManager.sceneLoaded += (scene, mode) => ApplySceneSettings(scene.name);
        ApplySceneSettings(SceneManager.GetActiveScene().name);
    }

    // The editor keeps QualitySettings changes after Play mode, so put the project's values back
    static void RestoreProjectSettings()
    {
        QualitySettings.renderPipeline = originalPipeline;
        QualitySettings.globalTextureMipmapLimit = originalMipLimit;
        QualitySettings.anisotropicFiltering = originalAnisotropic;
    }

    // Applies and saves everything (the settings window's Apply button)
    public static void ApplyAll()
    {
        Vector2Int size = Resolution;
        if (size.x != Screen.width || size.y != Screen.height || DisplayMode != Screen.fullScreenMode)
            Screen.SetResolution(size.x, size.y, DisplayMode);

        ApplyGraphics();
        AudioListener.volume = MasterVolume;
        ApplySceneSettings(SceneManager.GetActiveScene().name);
        foreach (WeaponHolder holder in Object.FindObjectsByType<WeaponHolder>(FindObjectsSortMode.None))
            holder.KeepLastKnife = KeepLastKnife;
        PlayerPrefs.Save();
    }

    static void ApplyGraphics()
    {
        if (runtimePipeline != null)
        {
            runtimePipeline.renderScale = RenderScaleOptions[RenderScaleIndex];
            runtimePipeline.msaaSampleCount = AntiAliasingOptions[AntiAliasingIndex];
            runtimePipeline.shadowDistance = ShadowDistanceOptions[ShadowIndex];
        }
        QualitySettings.globalTextureMipmapLimit = TextureMipLimitOptions[TextureIndex];
        QualitySettings.anisotropicFiltering = AnisotropicOptions[AnisotropicIndex];
    }

    static void ApplySceneSettings(string sceneName)
    {
        ApplyFrameRate(sceneName);
        if (!IsMenuScene(sceneName) && Camera.main != null) Camera.main.fieldOfView = FieldOfView;
    }

    public static void ApplyFrameRate(string sceneName)
    {
        if (IsMenuScene(sceneName))
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = MenuFrameRate;
            return;
        }

        QualitySettings.vSyncCount = VSync ? 1 : 0;
        int limit = FrameRateLimit;
        Application.targetFrameRate = limit > 0 ? limit : -1;
    }
}
