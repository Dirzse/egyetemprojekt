using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SettingId
{
    Resolution, DisplayMode, VSync, FrameRate,
    Preset, RenderScale, AntiAliasing, Shadows, Textures, Anisotropic,
    MouseSensitivity, InvertMouseY, FieldOfView, KeepLastKnife,
    MasterVolume,
}

// One "<  value  >" row of the settings window (made by Tools > UI > Build Settings Window).
// Which setting it changes is chosen by Id, so rows can be moved, restyled or removed freely.
public class SettingRow : MonoBehaviour
{
    public SettingId id;
    public TMP_Text valueText;
    public Button previousButton;
    public Button nextButton;
}
