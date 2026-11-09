using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrenadeThrower : Weapon
{
    [SerializeField] Transform throwPoint;
    [SerializeField] Grenade grenadePrefab;
    [SerializeField] float throwSpeed = 15f;
    [SerializeField] float upwardAngle = 8f;

    [Header("Ív")]
    [SerializeField] int maxPoints = 60;
    [SerializeField] float timeStep = 0.05f;
    [SerializeField] LayerMask hitMask = ~0;

    LineRenderer line;
    readonly List<Vector3> points = new List<Vector3>();

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.enabled = false;
    }

    protected override void OnAimChanged(bool aiming) => line.enabled = aiming;

    Vector3 ThrowVelocity()
    {
        Vector3 dir = Quaternion.AngleAxis(-upwardAngle, throwPoint.right) * throwPoint.forward;
        return dir * throwSpeed;
    }

    public override bool TryGetAmmoType(out AmmoType type) { type = AmmoType.Grenade; return true; }

    public override void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanUse && HasAmmo(AmmoType.Grenade))
        {
            if (ConsumeAmmo(AmmoType.Grenade) <= 0) return;
            nextUseTime = Time.time + cooldown;

            Grenade g = Instantiate(grenadePrefab, throwPoint.position, Quaternion.identity);
            g.Launch(ThrowVelocity());
        }
    }

    void LateUpdate()
    {
        if (!IsAiming) return;

        bool hasGrenade = HasAmmo(AmmoType.Grenade);
        line.enabled = hasGrenade;
        if (hasGrenade) DrawTrajectory();
    }

    void DrawTrajectory()
    {
        points.Clear();
        Vector3 pos = throwPoint.position;
        Vector3 vel = ThrowVelocity();
        Vector3 g = Physics.gravity;
        points.Add(pos);

        for (int i = 0; i < maxPoints; i++)
        {
            Vector3 next = pos + vel * timeStep + 0.5f * g * timeStep * timeStep;
            vel += g * timeStep;

            if (Physics.Linecast(pos, next, out RaycastHit hit, hitMask, QueryTriggerInteraction.Ignore))
            {
                points.Add(hit.point);
                break;
            }
            points.Add(next);
            pos = next;
        }

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }
}