using UnityEngine;
using TMPro; // TextMeshPro használatához
using UnityEngine.EventSystems; // Az egér eseményekhez kötelező
using UnityEngine.SceneManagement; // A jelenetváltáshoz kötelező
using System.Collections; // A késleltetéshez (Coroutine) szükséges

public class UIButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private TextMeshProUGUI buttonText;
    private Color originalColor;
    
    [Header("Scene / Kilépés Beállítások")]
    [Tooltip("A Scene pontos neve, amire át szeretnél váltani. Ha ide azt írod, hogy 'exit' vagy 'quit', a gomb bezárja a játékot!")]
    [SerializeField] private string targetSceneName = "GameScene";
    [Tooltip("Hány másodpercet várjon a művelet (váltás/kilépés) előtt a gombnyomás UTÁN.")]
    [SerializeField] private float changeSceneDelay = 0.5f;

    [Header("Védelem a véletlen kattintás ellen")]
    [Tooltip("Hány másodpercig legyen inaktív a gomb a Jelenet betöltése UTÁN (megelőzi a Splash képernyőről való átkattintást).")]
    [SerializeField] private float startCooldown = 0.5f; // Átírva 2.0-ról 0.5-re!

    [Header("Szín Beállítások")]
    [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f); // Arany szín

    [Header("Villogás Beállítások (Hover)")]
    [SerializeField] private float blinkSpeed = 10f; // Mennyire gyorsan villogjon ha rajta van az egér
    
    private bool isHovered = false;
    private bool isClicked = false;
    private bool isReady = false; // Jelzi, hogy letelt-e a kezdő várakozás

    void Awake()
    {
        buttonText = GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            originalColor = buttonText.color;
        }
    }

    void Start()
    {
        // Elindítjuk a kezdő időzítőt, ami blokkolja a gombot az elején
        StartCoroutine(EnableButtonAfterDelay());
    }

    void Update()
    {
        // Csak akkor fut a villogás, ha a gomb már aktív (letelt a 0.5 mp), az egér rajta van, és még nem kattintottak rá
        if (isReady && isHovered && !isClicked && buttonText != null)
        {
            float alpha = (Mathf.Sin(Time.time * blinkSpeed) + 1.0f) / 2.0f;
            Color tempColor = originalColor;
            tempColor.a = Mathf.Lerp(0.3f, 1.0f, alpha);
            buttonText.color = tempColor;
        }
    }

    // Kezdő időzítő: Megvárja a 0.5 másodpercet, és csak utána élesíti a gombot
    private IEnumerator EnableButtonAfterDelay()
    {
        isReady = false;
        yield return new WaitForSeconds(startCooldown);
        isReady = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isReady || isClicked) return;
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (isReady && !isClicked && buttonText != null) buttonText.color = originalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Ha még nem járt le a 0.5 másodperces türelmi idő, teljesen figyelmen kívül hagyja a kattintást!
        if (!isReady || isClicked) return;

        isClicked = true;
        isHovered = false;

        if (buttonText != null) buttonText.color = goldColor;

        StartCoroutine(ExecuteActionWithDelay());
    }

    private IEnumerator ExecuteActionWithDelay()
    {
        yield return new WaitForSeconds(changeSceneDelay);

        string trimmedName = targetSceneName.Trim().ToLower();
        if (trimmedName == "exit" || trimmedName == "quit")
        {
            Debug.Log("Játék leállítása...");
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
