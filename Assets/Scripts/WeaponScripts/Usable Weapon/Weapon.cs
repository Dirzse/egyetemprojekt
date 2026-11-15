using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    [SerializeField] protected float cooldown = 0.5f;

    [Header("HUD")]
    [SerializeField] string displayName;           // ha üres, az objektum neve lesz

    [Header("Célzás")]
    [SerializeField] bool canAim = false;
    [SerializeField] float aimFov = 45f;
    [SerializeField] bool showCrosshair = false;

    [Header("Draw / holster")]
    [Tooltip("Time to draw this weapon before it can be used (seconds). The character's draw speed divides it.")]
    [SerializeField] protected float drawTime = 0.4f;
    [Tooltip("Optional: a copy of the weapon model attached to a bone (e.g. the bow on spine_03, the sword on pelvis). " +
             "Shown while this weapon is NOT in the hand, so the character visibly carries it.")]
    [SerializeField] GameObject holsteredVisual;

    [Header("World pickup")]
    [Tooltip("The weapon lies in the world and can be picked up with F (it goes to the last slot and is in hand right away). " +
             "It can not be kept: switching to another weapon drops it. Give it a Rigidbody and a Collider.")]
    [SerializeField] bool worldPickup = false;
    [Tooltip("Position / rotation in the hand (relative to the WeaponHolder's pickup socket).")]
    [SerializeField] Vector3 holdPosition;
    [SerializeField] Vector3 holdRotation;

    protected float nextUseTime;
    float drawReadyTime;
    AmmoInventory inventory;
    WeaponHolder holder;
    bool isHeld;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    // Melyik lőszert használja a fegyver (a HUD ebből tudja, mit írjon ki). Alapból: semmit (pl. kard).
    public virtual bool TryGetAmmoType(out AmmoType type)
    {
        type = default;
        return false;
    }

    public bool CanAim => canAim;
    public float AimFov => aimFov;
    public bool ShowCrosshair => showCrosshair;
    public bool IsAiming { get; private set; }

    // A player AmmoInventory-ja (a fegyver a playernek a gyereke). Ha nincs, a lőszer végtelen.
    protected AmmoInventory Inventory
    {
        get
        {
            if (inventory == null) inventory = GetComponentInParent<AmmoInventory>(true);
            return inventory;
        }
    }

    protected bool HasAmmo(AmmoType type, int n = 1) => Inventory == null || Inventory.Has(type, n);

    // Visszaadja, hány darabot sikerült levonni
    protected int ConsumeAmmo(AmmoType type, int n = 1) => Inventory == null ? n : Inventory.Consume(type, n);

    public virtual void OnEquip()
    {
        gameObject.SetActive(true);
        if (holsteredVisual) holsteredVisual.SetActive(false);
        drawReadyTime = Time.time + drawTime / Mathf.Max(0.1f, DrawSpeedMultiplier);
    }

    public virtual void OnUnequip()
    {
        SetAiming(false);
        gameObject.SetActive(false);
        if (holsteredVisual) holsteredVisual.SetActive(true);
    }

    public void SetAiming(bool value)
    {
        value = value && canAim;
        if (value == IsAiming) return;
        IsAiming = value;
        OnAimChanged(value);
    }

    protected virtual void OnAimChanged(bool aiming) { }

    public abstract void HandleInput();

    protected bool CanUse => Time.time >= nextUseTime && IsDrawn;
    protected bool IsDrawn => Time.time >= drawReadyTime;

    // Stats of the character holding this weapon (null if there is no PlayerStats above it)
    protected PlayerStats OwnerStats => GetComponentInParent<PlayerStats>();
    protected float DamageMultiplier => OwnerStats != null ? OwnerStats.DamageMultiplier : 1f;
    protected float DrawSpeedMultiplier => OwnerStats != null ? OwnerStats.DrawSpeedMultiplier : 1f;
    protected float ThrowPowerMultiplier => OwnerStats != null ? OwnerStats.ThrowPowerMultiplier : 1f;

    // Value of the Animator's WeaponType parameter while this weapon is in the hand: 0 none, 1 bow, 2 sword, 3 knife, 4 gun
    public virtual int AnimationType => 0;

    // The Animator of the player's current model, found through the WeaponHolder (null if there is none)
    protected Animator CharacterAnimator
    {
        get
        {
            if (holder == null)
            {
                PlayerMovement player = GetComponentInParent<PlayerMovement>(true);
                if (player != null) holder = player.GetComponentInChildren<WeaponHolder>(true);
            }
            return holder != null ? holder.CharacterAnimator : null;
        }
    }

    // World pickup: only weapons that override these can be picked up and dropped (e.g. Gun)
    // (or any weapon with "World Pickup" ticked: it uses the default code below)
    public virtual bool CanBePickedUp => worldPickup && !isHeld;

    public virtual void OnPickedUp(Transform socket)
    {
        if (!worldPickup) return;
        isHeld = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body) body.isKinematic = true;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;

        transform.SetParent(socket, false);
        transform.localPosition = holdPosition;
        transform.localRotation = Quaternion.Euler(holdRotation);
    }

    public virtual void OnDropped(Vector3 position, Vector3 velocity)
    {
        if (!worldPickup) return;
        isHeld = false;

        gameObject.SetActive(true);   // it is hidden while another weapon is in hand
        transform.SetParent(null, true);
        transform.position = position;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body)
        {
            body.isKinematic = false;
            body.linearVelocity = velocity;   // before Unity 6: body.velocity
            body.angularVelocity = Random.insideUnitSphere * 3f;
        }
    }
}
