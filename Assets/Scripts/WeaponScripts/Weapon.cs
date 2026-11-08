using UnityEngine;

// Közös ős. A célzás-beállítások fegyverenként az Inspectorban állíthatók.
public abstract class Weapon : MonoBehaviour
{
    [SerializeField] protected float cooldown = 0.5f;

    [Header("Célzás")]
    [SerializeField] bool canAim = false;          // van-e jobb klikkes célzás / zoom
    [SerializeField] float aimFov = 45f;           // ennyi lesz a kamera FOV-ja célzás közben
    [SerializeField] bool showCrosshair = false;   // látszódjon-e a célkereszt

    protected float nextUseTime;

    public bool CanAim => canAim;
    public float AimFov => aimFov;
    public bool ShowCrosshair => showCrosshair;
    public bool IsAiming { get; private set; }

    public virtual void OnEquip() { gameObject.SetActive(true); }

    public virtual void OnUnequip()
    {
        SetAiming(false);
        gameObject.SetActive(false);
    }

    // A WeaponHolder hívja minden frame-ben
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