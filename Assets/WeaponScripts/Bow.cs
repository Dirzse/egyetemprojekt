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

    float chargeStart = -1f;

    public override void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanUse)
            chargeStart = Time.time;

        if (Input.GetMouseButtonUp(0) && chargeStart >= 0f)
        {
            float charge = Mathf.Clamp01((Time.time - chargeStart) / maxChargeTime);
            Fire(charge);
            chargeStart = -1f;
            nextUseTime = Time.time + cooldown;
        }
    }

    void Fire(float charge)
    {
        Vector3 dir = muzzle.forward;   // alapból mindig a cső irányába

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
