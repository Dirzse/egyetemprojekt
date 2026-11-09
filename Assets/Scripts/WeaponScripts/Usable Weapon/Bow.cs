using UnityEngine;

public class Bow : Weapon
{
    [Header("Referenciák")]
    [SerializeField] Transform muzzle;
    [SerializeField] Arrow arrowPrefab;
    [SerializeField] Camera playerCamera;

    [Header("Lövés")]
    [SerializeField] float minSpeed = 15f;
    [SerializeField] float maxSpeed = 45f;
    [SerializeField] float maxChargeTime = 1.0f;

    [Header("Opcionális")]
    [SerializeField] bool aimAtCrosshair = false;
    [SerializeField] LayerMask aimMask = ~0;

    float chargeStart = -1f;

    public override bool TryGetAmmoType(out AmmoType type) { type = AmmoType.Arrow; return true; }

    public override void HandleInput()
    {
        // Csak akkor kezd tölteni, ha van nyíl
        if (Input.GetMouseButtonDown(0) && CanUse && HasAmmo(AmmoType.Arrow))
            chargeStart = Time.time;

        if (Input.GetMouseButtonUp(0) && chargeStart >= 0f)
        {
            float charge = Mathf.Clamp01((Time.time - chargeStart) / maxChargeTime);
            if (ConsumeAmmo(AmmoType.Arrow) > 0) Fire(charge);
            chargeStart = -1f;
            nextUseTime = Time.time + cooldown;
        }
    }

    void Fire(float charge)
    {
        Vector3 dir = muzzle.forward;

        if (aimAtCrosshair && playerCamera != null &&
            Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                out RaycastHit hit, 200f, aimMask, QueryTriggerInteraction.Ignore))
        {
            dir = (hit.point - muzzle.position).normalized;
        }

        Arrow arrow = Instantiate(arrowPrefab, muzzle.position, Quaternion.LookRotation(dir));
        arrow.Launch(dir * Mathf.Lerp(minSpeed, maxSpeed, charge));
    }

    public override void OnUnequip()
    {
        chargeStart = -1f;
        base.OnUnequip();
    }
}