using UnityEngine;

// Közös ős: minden fegyver ebből származik.
public abstract class Weapon : MonoBehaviour
{
    [SerializeField] protected float cooldown = 0.5f;
    protected float nextUseTime;

    public virtual void OnEquip()   { gameObject.SetActive(true); }
    public virtual void OnUnequip() { gameObject.SetActive(false); }

    // Minden frame-ben a WeaponHolder hívja
    public abstract void HandleInput();

    protected bool CanUse => Time.time >= nextUseTime;
}
