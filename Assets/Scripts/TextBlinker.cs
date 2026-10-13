using UnityEngine;
using TMPro; // Required for TextMeshPro

public class TextBlinker : MonoBehaviour
{
    private TextMeshProUGUI textComponent;
    
    [Header("Blink Settings")]
    [Tooltip("How fast the text blinks. Higher = faster.")]
    [SerializeField] private float blinkSpeed = 2.0f;
    
    [Tooltip("The lowest visibility point (0 is completely invisible, 1 is fully visible).")]
    [SerializeField] private float minAlpha = 0.2f;

    void Start()
    {
        // Get the TextMeshPro component attached to this GameObject
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (textComponent != null)
        {
            // Calculate a value that smoothly goes back and forth between 0 and 1
            float lerpTime = (Mathf.Sin(Time.time * blinkSpeed) + 1.0f) / 2.0f;
            
            // Scale that value so it stays between our minimum alpha and maximum alpha (1.0)
            float newAlpha = Mathf.Lerp(minAlpha, 1.0f, lerpTime);
            
            // Apply the new alpha to the text color
            Color textColor = textComponent.color;
            textColor.a = newAlpha;
            textComponent.color = textColor;
        }
    }
}
