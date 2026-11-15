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

    // Falls back to the player's model Animator (also after the character swap)
    Animator Anim => animator && animator.isActiveAndEnabled ? animator : CharacterAnimator;
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

    public int KnifeCount => Knives;

    // With an AmmoInventory on the player the knives come from it (HUD, ammo boxes), otherwise from knifeCount
    int Knives => Inventory != null ? Inventory.Get(AmmoType.Knife) : knifeCount;

    public override bool TryGetAmmoType(out AmmoType type) { type = AmmoType.Knife; return true; }

    public override int AnimationType => 3;

    void Awake()
    {
        if (playerCamera) defaultFov = playerCamera.fieldOfView;
    }

    public override void HandleInput()
    {
        UpdateZoom();
        UpdateHandVisual();   // ammo boxes can add knives at any time

        if (Knives <= 0)
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
        if (Anim)
        {
            Anim.SetInteger(SlashSideHash, slashSide);
            Anim.SetTrigger(KnifeSlashHash);
        }
        slashSide = 1 - slashSide;   // next swing goes the other way
        Invoke(nameof(DoSlashHit), slashHitDelay);
    }

    void DoSlashHit()
    {
        Transform cam = playerCamera.transform;
        // Same as the sword: SphereCast forward from the camera
        if (DamageUtil.SphereCastIgnoring(cam.position, slashRadius, cam.forward, slashRange, hitMask, transform.root, out RaycastHit hit))
        {
            DamageUtil.Apply(hit.collider, slashDamage * DamageMultiplier);
        }
    }

    // ---------- Aim + throw ----------

    void StartAiming()
    {
        isAiming = true;
        if (Anim) Anim.SetBool(KnifeAimHash, true);
    }

    void StopAiming()
    {
        if (!isAiming) return;
        isAiming = false;
        if (Anim) Anim.SetBool(KnifeAimHash, false);
    }

    void Throw()
    {
        StopAiming();
        nextUseTime = Time.time + Mathf.Max(throwCooldown, releaseDelay);
        if (Anim) Anim.SetTrigger(ThrowHash);
        Invoke(nameof(Release), releaseDelay);
    }

    void Release()
    {
        if (Knives <= 0) return;
        if (Inventory != null) ConsumeAmmo(AmmoType.Knife);
        else knifeCount--;

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
        bool added;
        if (Inventory != null)
        {
            added = Inventory.Add(AmmoType.Knife, amount) > 0;
        }
        else
        {
            added = knifeCount < maxKnives;
            knifeCount = Mathf.Min(knifeCount + amount, maxKnives);
        }
        UpdateHandVisual();
        return added;
    }

    // Called by PlayerStats with the selected character's values
    public void SetKnives(int count, int max)
    {
        maxKnives = Mathf.Max(0, max);
        knifeCount = Mathf.Clamp(count, 0, maxKnives);

        // Starting knives of the character go into the AmmoInventory (its own max stays)
        if (Inventory != null)
        {
            Inventory.Consume(AmmoType.Knife, Inventory.Get(AmmoType.Knife));
            Inventory.Add(AmmoType.Knife, knifeCount);
        }
        UpdateHandVisual();
    }

    void UpdateHandVisual()
    {
        bool inHand = gameObject.activeSelf && Knives > 0;   // active = equipped
        if (knifeInHand) knifeInHand.SetActive(inHand);
        // The strap shows the rest: every knife, minus the one in the hand
        if (holster) holster.Show(inHand ? Knives - 1 : Knives);
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
        if (Anim)
        {
            Anim.ResetTrigger(KnifeSlashHash);
            Anim.ResetTrigger(ThrowHash);
        }
        base.OnUnequip();
        UpdateHandVisual();   // the knife from the hand goes back to the strap
    }
}