using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    [SerializeField] float lifetime = 10f;
    [SerializeField] float damage = 25f;
    [Tooltip("Thickness of the hit check along the flight path. It does not depend on the arrow's collider, so fast arrows do not fly through enemies.")]
    [SerializeField] float hitRadius = 0.05f;
    [SerializeField] LayerMask hitMask = ~0;

    Rigidbody rb;
    bool stuck;
    Transform ownerRoot;

    static readonly RaycastHit[] pathHits = new RaycastHit[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // gyors nyílnál fontos
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Launch(Vector3 velocity, float damageMultiplier = 1f, Transform owner = null)
    {
        damage *= damageMultiplier;

        ownerRoot = owner;

        // Never hits the player who shot it (it spawns next to the player's colliders)
        Collider own = GetComponent<Collider>();
        if (owner != null && own != null)
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(own, ownerCollider);
        }

        rb.linearVelocity = velocity;   // Unity 6 előtt: rb.velocity
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        if (stuck) return;

        // Checks the path of this physics step, so the arrow cannot skip through a target between two steps
        Vector3 step = rb.linearVelocity * Time.fixedDeltaTime;
        float length = step.magnitude;
        if (length > 0.0001f && FindHit(step / length, length, out RaycastHit hit))
        {
            transform.position = hit.distance > 0f ? hit.point : rb.position;
            Hit(hit.collider);
            return;
        }

        // Repülés közben a nyíl a haladási irányába fordul
        if (rb.linearVelocity.sqrMagnitude > 0.1f)
            rb.MoveRotation(Quaternion.LookRotation(rb.linearVelocity));
    }

    // Closest hit on the path that is not the arrow itself or the player who shot it
    bool FindHit(Vector3 direction, float length, out RaycastHit result)
    {
        int count = Physics.SphereCastNonAlloc(rb.position, hitRadius, direction, pathHits, length, hitMask, QueryTriggerInteraction.Ignore);
        result = default;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Transform t = pathHits[i].transform;
            if (t == transform || t.IsChildOf(transform)) continue;
            if (ownerRoot != null && t.IsChildOf(ownerRoot)) continue;
            if (pathHits[i].distance >= best) continue;
            best = pathHits[i].distance;
            result = pathHits[i];
        }
        return best < float.MaxValue;
    }

    void OnCollisionEnter(Collision col)
    {
        Hit(col.collider);
    }

    void Hit(Collider target)
    {
        if (stuck) return;
        stuck = true;

        // Sebzés, ha van rajta valami (saját interfésszel bővítheted)
        // Upwards: also reaches the EnemyAI when a child collider of the enemy is hit
        target.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        // Beleragad a célba
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        GetComponent<Collider>().enabled = false;
        transform.SetParent(target.transform, true);
    }
}