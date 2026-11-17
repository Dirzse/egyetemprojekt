using UnityEngine;

public class EnemyArrow : MonoBehaviour
{
    public float damage = 15f;
    public float lifeTime = 5f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Másik enemy-t vagy másik enemy nyilát hagyja figyelmen kívül
        if (other.GetComponentInParent<EnemyAI>() != null) return;
        if (other.GetComponentInParent<EnemyArrow>() != null) return;

        // Ha a játékost találta el, sebezze meg
        CharacterHealth health = other.GetComponentInParent<CharacterHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Ha valódi falnak/talajnak ütközik (csak a Default vagy pályaelem rétegek, ne a játékos fegyvere/lövedéke)
        if (!other.isTrigger && other.attachedRigidbody == null)
        {
            Destroy(gameObject);
        }
    }
}