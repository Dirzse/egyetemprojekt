using UnityEngine;

// Caps the frame rate in every scene. Runs by itself at startup, no need to put it on any object.
// The limit comes from the settings (GameSettings): 60 FPS in the menus, the chosen limit in the game.
public static class FrameRateLimiter
{
    // 0 = the monitor's refresh rate
    // (not used any more: the settings window's FPS limit and GameSettings decide)
    const int MaxFrameRate = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        GameSettings.Initialize();
        Settings.CreateInstance();
    }
}
