using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    [SerializeField] float lifetime = 10f;
    [SerializeField] float damage = 25f;

    Rigidbody rb;
    bool stuck;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // gyors nyílnál fontos
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Launch(Vector3 velocity, float damageMultiplier = 1f, Transform owner = null)
    {
        damage *= damageMultiplier;

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
        // Repülés közben a nyíl a haladási irányába fordul
        if (!stuck && rb.linearVelocity.sqrMagnitude > 0.1f)
            rb.MoveRotation(Quaternion.LookRotation(rb.linearVelocity));
    }

    void OnCollisionEnter(Collision col)
    {
        if (stuck) return;
        stuck = true;

        // Sebzés, ha van rajta valami (saját interfésszel bővítheted)
        // Upwards: also reaches the EnemyAI when a child collider of the enemy is hit
        col.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        // Beleragad a célba
        rb.isKinematic = true;
        GetComponent<Collider>().enabled = false;
        transform.SetParent(col.transform, true);
    }
}