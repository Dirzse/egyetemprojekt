using UnityEngine;

// Caps the frame rate in every scene. Runs by itself at startup, no need to put it on any object.
public static class FrameRateLimiter
{
    // 0 = the monitor's refresh rate
    const int MaxFrameRate = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;

        int rate = MaxFrameRate;
        if (rate <= 0) rate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
        if (rate <= 0) rate = 60;

        Application.targetFrameRate = rate;
    }
}
