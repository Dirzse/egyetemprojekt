using UnityEngine;

// Knife: LMB = slash (alternating left/right swings, like the sword),
// hold RMB = aim, release RMB = throw. Both only work while you have knives.
// Thrown knives can be picked back up with PlayerInteract (F).
public class ThrowingKnife : Weapon
{
    [Header("References")]
    [SerializeField] Camera playerCamera;          // aiming (crosshair) and slash direction
    [SerializeField] Transform throwPoint;         // empty object in the hand, the knife starts here
    [SerializeField] ThrownKnife knifePrefab;      // Rigidbody + Collider + ThrownKnife script
    [SerializeField] GameObject knifeInHand;       // the knife model in the hand, hidden when out of knives
    [SerializeField] Animator animator;            // optional: KnifeSlash, SlashSide, KnifeAim, Throw
    [Tooltip("Optional: knives on the body (thigh strap). The one in the hand is not counted in it.")]
    [SerializeField] KnifeHolster holster;

    [Header("Knives (overwritten by the selected character through PlayerStats)")]
    [SerializeField] int knifeCount = 3;
    [SerializeField] int maxKnives = 5;

    [Header("Slash - LMB (cooldown = time between slashes)")]
    [SerializeField] float slashDamage = 15f;
    [SerializeField] float slashRange = 1.6f;
    [SerializeField] float slashRadius = 0.4f;
    [Tooltip("Hit moment inside the slash animation.")]
    [SerializeField] float slashHitDelay = 0.12f;
    [SerializeField] LayerMask hitMask = ~0;

    [Header("Throw - hold RMB to aim, release to throw")]
    [SerializeField] float throwSpeed = 25f;
    [SerializeField] float throwDamage = 40f;
    [Tooltip("Time from the start of the throw animation to the moment the knife leaves the hand.")]
    [SerializeField] float releaseDelay = 0.25f;
    [SerializeField] float throwCooldown = 0.6f;
    [SerializeField] LayerMask aimMask = ~0;

    [Header("Aim zoom (optional, 0 = off)")]
    [SerializeField] float throwZoomFov = 0f;
    [SerializeField] float zoomSpeed = 10f;

    static readonly int KnifeSlashHash = Animator.StringToHash("KnifeSlash");
    static readonly int SlashSideHash = Animator.StringToHash("SlashSide");
    static readonly int KnifeAimHash = Animator.StringToHash("KnifeAim");
    static readonly int ThrowHash = Animator.StringToHash("Throw");

    bool isAiming;
    int slashSide;          // 0 = right to left, 1 = left to right
    float defaultFov;

    public int KnifeCount => knifeCount;

    void Awake()
    {
        if (playerCamera) defaultFov = playerCamera.fieldOfView;
    }

    public override void HandleInput()
    {
        UpdateZoom();

        if (knifeCount <= 0)
        {
            StopAiming();
            return;
        }

        // Aiming: holding RMB starts it (also if it was held during the draw)
        if (!isAiming && Input.GetMouseButton(1) && CanUse) StartAiming();

        if (isAiming)
        {
            if (!Input.GetMouseButton(1)) Throw();
            return;   // no slashing while aiming
        }

        if (Input.GetMouseButtonDown(0) && CanUse) Slash();
    }

    // ---------- Slash ----------

    void Slash()
    {
        nextUseTime = Time.time + cooldown;
        if (animator)
        {
            animator.SetInteger(SlashSideHash, slashSide);
            animator.SetTrigger(KnifeSlashHash);
        }
        slashSide = 1 - slashSide;   // next swing goes the other way
        Invoke(nameof(DoSlashHit), slashHitDelay);
    }

    void DoSlashHit()
    {
        Transform cam = playerCamera.transform;
        // Same as the sword: SphereCast forward from the camera
        if (Physics.SphereCast(cam.position, slashRadius, cam.forward, out RaycastHit hit,
                               slashRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            hit.collider.SendMessage("TakeDamage", slashDamage * DamageMultiplier, SendMessageOptions.DontRequireReceiver);
        }
    }

    // ---------- Aim + throw ----------

    void StartAiming()
    {
        isAiming = true;
        if (animator) animator.SetBool(KnifeAimHash, true);
    }

    void StopAiming()
    {
        if (!isAiming) return;
        isAiming = false;
        if (animator) animator.SetBool(KnifeAimHash, false);
    }

    void Throw()
    {
        StopAiming();
        nextUseTime = Time.time + Mathf.Max(throwCooldown, releaseDelay);
        if (animator) animator.SetTrigger(ThrowHash);
        Invoke(nameof(Release), releaseDelay);
    }

    void Release()
    {
        if (knifeCount <= 0) return;
        knifeCount--;

        // Aim at what is under the crosshair, but start from the hand
        Transform cam = playerCamera.transform;
        Vector3 target = cam.position + cam.forward * 100f;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 100f, aimMask, QueryTriggerInteraction.Ignore))
        {
            target = hit.point;
        }
        Vector3 dir = (target - throwPoint.position).normalized;

        ThrownKnife knife = Instantiate(knifePrefab, throwPoint.position, Quaternion.LookRotation(dir));
        knife.Launch(dir * throwSpeed * ThrowPowerMultiplier, throwDamage * DamageMultiplier, transform.root);

        UpdateHandVisual();
    }

    void UpdateZoom()
    {
        if (playerCamera == null || throwZoomFov <= 0f) return;
        float target = isAiming ? throwZoomFov : defaultFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, zoomSpeed * Time.deltaTime);
    }

    // ---------- Knife count ----------

    // Returns false if the player already carries the maximum
    public bool AddKnives(int amount)
    {
        if (knifeCount >= maxKnives) return false;
        knifeCount = Mathf.Min(knifeCount + amount, maxKnives);
        UpdateHandVisual();
        return true;
    }

    // Called by PlayerStats with the selected character's values
    public void SetKnives(int count, int max)
    {
        maxKnives = Mathf.Max(0, max);
        knifeCount = Mathf.Clamp(count, 0, maxKnives);
        UpdateHandVisual();
    }

    void UpdateHandVisual()
    {
        bool inHand = gameObject.activeSelf && knifeCount > 0;   // active = equipped
        if (knifeInHand) knifeInHand.SetActive(inHand);
        // The strap shows the rest: every knife, minus the one in the hand
        if (holster) holster.Show(inHand ? knifeCount - 1 : knifeCount);
    }

    // ---------- Equip ----------

    public override void OnEquip()
    {
        base.OnEquip();
        UpdateHandVisual();
    }

    public override void OnUnequip()
    {
        // Switching away cancels a slash or a throw that has not happened yet (no knife is lost)
        CancelInvoke();
        StopAiming();
        if (playerCamera && throwZoomFov > 0f) playerCamera.fieldOfView = defaultFov;
        if (animator)
        {
            animator.ResetTrigger(KnifeSlashHash);
            animator.ResetTrigger(ThrowHash);
        }
        base.OnUnequip();
        UpdateHandVisual();   // the knife from the hand goes back to the strap
    }
}