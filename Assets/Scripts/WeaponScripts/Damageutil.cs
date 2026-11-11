using UnityEngine;

// Shared damage helpers for every weapon.
public static class DamageUtil
{
    static readonly RaycastHit[] hits = new RaycastHit[16];

    // Sends TakeDamage to the hit object AND its parents, so hitting a child collider
    // (e.g. a sphere on the enemy) still reaches the EnemyAI on the root.
    public static void Apply(Collider target, float amount)
    {
        if (target == null) return;
        target.SendMessageUpwards("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
    }

    // Closest SphereCast hit that does not belong to 'ignoreRoot' (the attacker's own body and weapons)
    public static bool SphereCastIgnoring(Vector3 origin, float radius, Vector3 direction, float range,
                                          int mask, Transform ignoreRoot, out RaycastHit result)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, range, mask, QueryTriggerInteraction.Ignore);

        result = default;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (ignoreRoot != null && hits[i].transform.IsChildOf(ignoreRoot)) continue;
            if (hits[i].distance >= best) continue;
            best = hits[i].distance;
            result = hits[i];
            found = true;
        }

        // Hits that already overlap at the start have no point: use the closest point on the collider
        if (found && result.distance <= 0f)
        {
            result.point = result.collider.ClosestPoint(origin);
            result.normal = (origin - result.point).sqrMagnitude > 0.0001f ? (origin - result.point).normalized : -direction;
        }
        return found;
    }

    // Makes a projectile ignore every collider of its owner (it spawns inside or next to the player)
    public static void IgnoreOwner(Collider projectile, Transform owner)
    {
        if (projectile == null || owner == null) return;
        foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>(true))
            Physics.IgnoreCollision(projectile, ownerCollider);
    }
}