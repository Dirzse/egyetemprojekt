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

    protected float nextUseTime;
    AmmoInventory inventory;

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

    public virtual void OnEquip() { gameObject.SetActive(true); }

    public virtual void OnUnequip()
    {
        SetAiming(false);
        gameObject.SetActive(false);
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

    protected bool CanUse => Time.time >= nextUseTime;
}