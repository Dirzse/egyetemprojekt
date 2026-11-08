using System.Collections.Generic;
using UnityEngine;

// Gránát fegyver: jobb klikk = célzás + ív kirajzolása, bal klikk = dobás.
[RequireComponent(typeof(LineRenderer))]
public class GrenadeThrower : Weapon
{
    [SerializeField] Transform throwPoint;
    [SerializeField] Grenade grenadePrefab;
    [SerializeField] float throwSpeed = 15f;
    [SerializeField] float upwardAngle = 8f;       // enyhe felfelé dobás, hogy szép ívet adjon
    [SerializeField] int ammo = 3;

    [Header("Ív")]
    [SerializeField] int maxPoints = 60;
    [SerializeField] float timeStep = 0.05f;
    [SerializeField] LayerMask hitMask = ~0;       // mibe ütközzön az ív (a Player layert vedd ki!)

    LineRenderer line;
    readonly List<Vector3> points = new List<Vector3>();

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.enabled = false;
    }

    protected override void OnAimChanged(bool aiming) => line.enabled = aiming;

    // Ugyanazt a sebességet használja az ív és a tényleges dobás
    Vector3 ThrowVelocity()
    {
        Vector3 dir = Quaternion.AngleAxis(-upwardAngle, throwPoint.right) * throwPoint.forward;
        return dir * throwSpeed;
    }

    public override void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanUse && ammo > 0)
        {
            ammo--;
            nextUseTime = Time.time + cooldown;

            Vector3 v = ThrowVelocity();
            Grenade g = Instantiate(grenadePrefab, throwPoint.position, Quaternion.identity);
            g.Launch(v);
        }
    }

    void LateUpdate()
    {
        if (!IsAiming) return;
        line.enabled = ammo > 0;
        if (ammo > 0) DrawTrajectory();
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
                points.Add(hit.point);   // itt ér földet / falat
                break;
            }
            points.Add(next);
            pos = next;
        }

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }
}