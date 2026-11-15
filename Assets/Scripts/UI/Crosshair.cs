using UnityEngine;

// Crosshair (made by Tools > UI > Build Crosshair, edit it like any UI).
//  - A small dot in the middle all the time.
//  - Aim lines when aiming: bow (right click or drawing), knife throw (right click), always with a gun.
//    They open up while moving / jumping and close in while the bow string is pulled.
//  - A hit marker flashes when an enemy is hit (red when it dies).
// Hidden while the game is paused or the settings window is open.
public class Crosshair : MonoBehaviour
{
    [SerializeField] WeaponHolder weaponHolder;      // found automatically if empty
    [SerializeField] PlayerMovement movement;        // found automatically if empty

    [Header("Parts")]
    [Tooltip("Everything; hidden while paused.")]
    [SerializeField] CanvasGroup root;
    [Tooltip("The four aim lines' parent (faded in and out).")]
    [SerializeField] CanvasGroup aimLines;
    [SerializeField] RectTransform lineUp;
    [SerializeField] RectTransform lineDown;
    [SerializeField] RectTransform lineLeft;
    [SerializeField] RectTransform lineRight;
    [SerializeField] CanvasGroup hitMarker;
    [SerializeField] UnityEngine.UI.Graphic[] hitMarkerGraphics;

    [Header("Aim lines")]
    [Tooltip("Gap from the middle to the lines while aiming still (pixels).")]
    [SerializeField] float aimGap = 10f;
    [Tooltip("Gap when aiming starts, it closes in to aimGap.")]
    [SerializeField] float startGap = 30f;
    [Tooltip("Gap with the bow fully drawn.")]
    [SerializeField] float fullDrawGap = 3f;
    [SerializeField] float walkSpread = 10f;
    [SerializeField] float runSpread = 22f;
    [SerializeField] float airSpread = 18f;
    [SerializeField] float gapSpeed = 12f;
    [SerializeField] float fadeSpeed = 12f;

    [Header("Hit marker")]
    [SerializeField] float hitMarkerTime = 0.2f;
    [SerializeField] Color hitColor = Color.white;
    [SerializeField] Color killColor = new Color(0.9f, 0.3f, 0.28f);

    float gap;
    bool wasAiming;
    float hitTime = -10f;

    void OnEnable()  { EnemyAI.Damaged += OnEnemyDamaged; }
    void OnDisable() { EnemyAI.Damaged -= OnEnemyDamaged; }

    void Start()
    {
        gap = startGap;
        if (aimLines != null) aimLines.alpha = 0f;
        if (hitMarker != null) hitMarker.alpha = 0f;
    }

    void Update()
    {
        if (weaponHolder == null) weaponHolder = FindFirstObjectByType<WeaponHolder>();
        if (movement == null && weaponHolder != null) movement = weaponHolder.GetComponentInParent<PlayerMovement>();

        bool hidden = Time.timeScale == 0f || Settings.IsOpen;
        if (root != null) root.alpha = hidden ? 0f : 1f;

        float targetGap = aimGap;
        bool aiming = IsAiming(ref targetGap);
        if (aiming && !wasAiming) gap = startGap;   // the lines close in when aiming starts
        wasAiming = aiming;

        if (movement != null)
        {
            if (!movement.IsGrounded) targetGap += airSpread;
            else if (movement.MoveInput.sqrMagnitude > 0.01f) targetGap += movement.IsRunning ? runSpread : walkSpread;
        }

        gap = Mathf.Lerp(gap, targetGap, 1f - Mathf.Exp(-gapSpeed * Time.unscaledDeltaTime));
        PlaceLines(gap);

        if (aimLines != null)
            aimLines.alpha = Mathf.MoveTowards(aimLines.alpha, aiming ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);

        if (hitMarker != null)
        {
            float since = Time.time - hitTime;
            hitMarker.alpha = since < hitMarkerTime ? 1f - since / hitMarkerTime : 0f;
        }
    }

    bool IsAiming(ref float targetGap)
    {
        Weapon weapon = weaponHolder != null ? weaponHolder.Current : null;
        if (weapon == null) return false;

        if (weapon is Bow bow)
        {
            if (bow.IsDrawing) targetGap = Mathf.Lerp(aimGap, fullDrawGap, bow.Charge);
            return bow.IsDrawing || Input.GetMouseButton(1);
        }
        if (weapon is ThrowingKnife knife) return knife.IsAimingThrow;
        return weapon.AnimationType == 4;   // gun: always
    }

    void PlaceLines(float distance)
    {
        Place(lineUp, new Vector2(0f, 1f), distance);
        Place(lineDown, new Vector2(0f, -1f), distance);
        Place(lineLeft, new Vector2(-1f, 0f), distance);
        Place(lineRight, new Vector2(1f, 0f), distance);
    }

    static void Place(RectTransform line, Vector2 direction, float distance)
    {
        if (line == null) return;
        float halfLength = Mathf.Max(line.sizeDelta.x, line.sizeDelta.y) * 0.5f;
        line.anchoredPosition = direction * (distance + halfLength);
    }

    void OnEnemyDamaged(EnemyAI enemy, bool killed)
    {
        hitTime = Time.time;
        if (hitMarkerGraphics == null) return;
        foreach (UnityEngine.UI.Graphic graphic in hitMarkerGraphics)
            if (graphic != null) graphic.color = killed ? killColor : hitColor;
    }
}
