using UnityEngine;

public class Bow : Weapon
{
    [Header("Referenciák")]
    [SerializeField] Transform muzzle;          // üres GameObject az íj "csöve" (Z tengely = lövés iránya)
    [SerializeField] Arrow arrowPrefab;         // Rigidbody + Collider + Arrow script
    [SerializeField] Camera playerCamera;       // csak az opcionális célzáshoz kell

    [Header("Lövés")]
    [SerializeField] float minSpeed = 15f;
    [SerializeField] float maxSpeed = 45f;
    [SerializeField] float maxChargeTime = 1.0f;

    [Header("Opcionális")]
    [Tooltip("Ha be van kapcsolva, a nyíl a crosshair felé száll (a csőből indulva). Ki = pontosan a cső irányába.")]
    [SerializeField] bool aimAtCrosshair = false;
    [SerializeField] LayerMask aimMask = ~0;

    [Header("Animation")]
    [Tooltip("The character's Animator (Survival_Character). Optional: if empty, no animation is played.")]
    [SerializeField] Animator animator;

    // Falls back to the player's model Animator (also after the character swap)
    Animator Anim => animator && animator.isActiveAndEnabled ? animator : CharacterAnimator;

    static readonly int BowDrawHash = Animator.StringToHash("BowDraw");
    static readonly int BowFireHash = Animator.StringToHash("BowFire");

    float chargeStart = -1f;

    public override bool TryGetAmmoType(out AmmoType type) { type = AmmoType.Arrow; return true; }

    public override int AnimationType => 1;

    // For the crosshair: is the string being pulled, and how far (0-1)
    public bool IsDrawing => chargeStart >= 0f;
    public float Charge => IsDrawing ? Mathf.Clamp01((Time.time - chargeStart) / maxChargeTime) : 0f;

    public override void HandleInput()
    {
        // Csak akkor kezd tölteni, ha van nyíl
        if (Input.GetMouseButtonDown(0) && CanUse && HasAmmo(AmmoType.Arrow))
        {
            chargeStart = Time.time;
            if (Anim) Anim.SetBool(BowDrawHash, true);
        }

        if (Input.GetMouseButtonUp(0) && chargeStart >= 0f)
        {
            float charge = Mathf.Clamp01((Time.time - chargeStart) / maxChargeTime);
            if (Anim)
            {
                Anim.SetBool(BowDrawHash, false);
                Anim.SetTrigger(BowFireHash);
            }
            if (ConsumeAmmo(AmmoType.Arrow) > 0) Fire(charge);
            chargeStart = -1f;
            nextUseTime = Time.time + cooldown;
        }
    }

    void Fire(float charge)
    {
        Vector3 dir = muzzle.forward;   // alapból mindig a cső irányába

        Camera view = playerCamera != null ? playerCamera : PlayerCamera;   // a spawned prefab has no camera reference
        // In third person always towards the crosshair: the bow is not where the camera looks from
        if (view != null && (aimAtCrosshair || AimUtil.IsThirdPerson(view)))
        {
            Vector3 target = AimUtil.AimPoint(view, transform.root, aimMask, 200f);
            dir = (target - muzzle.position).normalized;
        }

        Arrow arrow = Instantiate(arrowPrefab, muzzle.position, Quaternion.LookRotation(dir));
        // ThrowPowerMultiplier = how strong the character's bow string is
        arrow.Launch(dir * Mathf.Lerp(minSpeed, maxSpeed, charge) * ThrowPowerMultiplier, DamageMultiplier, transform.root);
    }

    public override void OnUnequip()
    {
        chargeStart = -1f;
        if (Anim)
        {
            // Cancel a draw in progress when switching weapons
            Anim.SetBool(BowDrawHash, false);
            Anim.ResetTrigger(BowFireHash);
        }
        base.OnUnequip();
    }
}