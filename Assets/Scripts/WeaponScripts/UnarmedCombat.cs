using UnityEngine;
using UnityEngine.InputSystem;

// Fist attacks while no weapon is in the hand. The WeaponHolder adds and calls it automatically.
// Left click = punch, right and left hand in turn while clicking quickly.
// The button is an Input Action like the movement (e.g. Player/Attack); left mouse button if it is not set.
public class UnarmedCombat : MonoBehaviour
{
    [SerializeField] Camera playerCamera;

    [Tooltip("Punch button, e.g. Player/Attack. Empty = left mouse button.")]
    [SerializeField] InputActionReference attackAction;

    [Header("Punch")]
    [SerializeField] float damage = 20f;
    [SerializeField] float range = 1.6f;
    [SerializeField] float radius = 0.35f;
    [Tooltip("Time from the click to the hit (the fist reaching forward in the animation).")]
    [SerializeField] float hitDelay = 0.15f;
    [Tooltip("Minimum time between two punches.")]
    [SerializeField] float punchInterval = 0.45f;
    [Tooltip("Clicking again within this time punches with the other hand, otherwise the combo starts with the right hand.")]
    [SerializeField] float comboTime = 0.9f;
    [SerializeField] LayerMask hitMask = ~0;

    [Header("Effect")]
    [SerializeField] GameObject hitEffect;
    [SerializeField] AudioClip hitSound;

    static readonly int PunchHash = Animator.StringToHash("Punch");
    static readonly int PunchSideHash = Animator.StringToHash("PunchSide");

    WeaponHolder holder;
    float nextPunchTime;
    float lastPunchTime = -10f;
    int side;   // 0 = right hand, 1 = left hand

    void Awake()
    {
        holder = GetComponent<WeaponHolder>();
    }

    bool HasAction => attackAction != null && attackAction.action != null;

    void OnEnable()  { if (HasAction) attackAction.action.Enable(); }
    void OnDisable() { if (HasAction) attackAction.action.Disable(); }

    bool AttackPressed => HasAction ? attackAction.action.WasPressedThisFrame() : Input.GetMouseButtonDown(0);

    public void HandleInput()
    {
        if (!AttackPressed || Time.time < nextPunchTime) return;

        side = Time.time - lastPunchTime <= comboTime ? 1 - side : 0;
        lastPunchTime = Time.time;
        nextPunchTime = Time.time + punchInterval;

        Animator anim = holder != null ? holder.CharacterAnimator : null;
        if (anim)
        {
            anim.SetInteger(PunchSideHash, side);
            anim.SetTrigger(PunchHash);
        }
        Invoke(nameof(DoHit), hitDelay);
    }

    // A weapon was drawn before the punch landed
    public void Cancel()
    {
        CancelInvoke(nameof(DoHit));
        Animator anim = holder != null ? holder.CharacterAnimator : null;
        if (anim) anim.ResetTrigger(PunchHash);
    }

    void DoHit()
    {
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null) return;

        PlayerStats stats = GetComponentInParent<PlayerStats>();
        float multiplier = stats != null ? stats.DamageMultiplier : 1f;

        PlayerMovement player = GetComponentInParent<PlayerMovement>();
        Transform owner = player != null ? player.transform : transform.root;   // skips the player's own colliders

        if (DamageUtil.SphereCastIgnoring(cam.transform.position, radius, cam.transform.forward, range, hitMask, owner, out RaycastHit hit))
        {
            DamageUtil.Apply(hit.collider, damage * multiplier);

            if (hitEffect)
            {
                GameObject fx = Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, 3f);
            }
            if (hitSound) AudioSource.PlayClipAtPoint(hitSound, hit.point);
        }
    }
}