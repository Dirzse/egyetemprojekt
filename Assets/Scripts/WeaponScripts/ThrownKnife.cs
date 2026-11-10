using UnityEngine;

// The flying knife (prefab). It spins while flying, sticks into what it hits,
// and can be picked back up afterwards. The blade should point along the prefab's Z axis.
[RequireComponent(typeof(Rigidbody))]
public class ThrownKnife : MonoBehaviour
{
    [SerializeField] float lifetime = 60f;     // removed after this if nobody picks it up
    [SerializeField] bool spin = true;
    [SerializeField] float spinSpeed = 20f;    // radians per second around the knife's X axis

    Rigidbody rb;
    Collider col;
    float damage;
    bool stuck;

    public bool CanCollect => stuck;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponentInChildren<Collider>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // fast object
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Launch(Vector3 velocity, float hitDamage, Transform owner)
    {
        damage = hitDamage;

        // Do not hit the player who threw it (it starts inside their capsule)
        if (owner != null && col != null)
        {
            foreach (Collider ownerCol in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(col, ownerCol);
        }

        rb.linearVelocity = velocity;
        if (spin) rb.angularVelocity = transform.right * spinSpeed;
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (stuck) return;
        stuck = true;

        collision.collider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        // Stick into the target; the collider stays on so it can be picked up with F
        rb.isKinematic = true;
        transform.SetParent(collision.transform, true);
    }
}
