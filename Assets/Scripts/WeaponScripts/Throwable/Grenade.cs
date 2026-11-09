using UnityEngine;

// A gránát prefabra kerül (Rigidbody + Collider + ez a script).
[RequireComponent(typeof(Rigidbody))]
public class Grenade : MonoBehaviour
{
    [SerializeField] float fuse = 3f;            // mp a robbanásig
    [SerializeField] float radius = 6f;
    [SerializeField] float explosionForce = 15f;
    [SerializeField] float maxDamage = 100f;
    [SerializeField] GameObject explosionEffect; // opcionális (particle prefab)

    public void Launch(Vector3 velocity)
    {
        GetComponent<Rigidbody>().linearVelocity = velocity;   // Unity 6 előtt: .velocity
        Invoke(nameof(Explode), fuse);
    }

    void Explode()
    {
        Vector3 pos = transform.position;

        foreach (Collider col in Physics.OverlapSphere(pos, radius))
        {
            if (col.attachedRigidbody)
                col.attachedRigidbody.AddExplosionForce(explosionForce, pos, radius, 1f, ForceMode.Impulse);

            float dist = Vector3.Distance(pos, col.bounds.ClosestPoint(pos));
            float dmg = maxDamage * (1f - Mathf.Clamp01(dist / radius));
            col.SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);
        }

        if (explosionEffect) Instantiate(explosionEffect, pos, Quaternion.identity);
        Destroy(gameObject);
    }
}