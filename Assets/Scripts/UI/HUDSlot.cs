using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One weapon tile of the HUD (made by Tools > UI > Build Game HUD). GameHUD fills it every frame.
public class HUDSlot : MonoBehaviour
{
    public Image background;
    public Image underline;
    public Image icon;
    public TMP_Text count;
    public TMP_Text key;

    [HideInInspector] public float scale = 1f;

    public RectTransform Rect => (RectTransform)transform;
}
