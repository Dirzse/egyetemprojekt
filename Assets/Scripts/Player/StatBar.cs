using UnityEngine;
using TMPro;

// One stat row on the character select screen (label, value, bar). The bar slides to its new value.
public class StatBar : MonoBehaviour
{
    [Tooltip("Anchored to the left edge; its width is set through anchorMax.x (0..1).")]
    [SerializeField] RectTransform fill;
    [SerializeField] TMP_Text valueText;
    [SerializeField] float animSpeed = 8f;

    float target;
    float current;

    public void SetValue(float normalized, string text)
    {
        target = Mathf.Clamp01(normalized);
        if (valueText) valueText.text = text;
    }

    void Update()
    {
        current = Mathf.Lerp(current, target, animSpeed * Time.unscaledDeltaTime);
        if (fill)
        {
            Vector2 max = fill.anchorMax;
            max.x = current;
            fill.anchorMax = max;
        }
    }
}
