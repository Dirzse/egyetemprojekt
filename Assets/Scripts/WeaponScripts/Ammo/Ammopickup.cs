using System;
using UnityEngine;

// Ammo box: Trigger collider + ez a script. Felvételkor lőszert ad az AmmoInventory-nak.
public class AmmoPickup : MonoBehaviour
{
    [Serializable]
    public class Amount
    {
        public AmmoType type;
        public int amount;
    }

    [SerializeField]
    Amount[] contents =
    {
        new Amount { type = AmmoType.Arrow,   amount = 20 },
        new Amount { type = AmmoType.Knife,   amount = 21 },   // 7 dobás
        new Amount { type = AmmoType.Grenade, amount = 5  },
    };

    [Header("Működés")]
    [SerializeField] float respawnTime = 0f;     // 0 = egyszer használatos, >0 = ennyi mp múlva újra felvehető
    [SerializeField] AudioClip pickupSound;

    [Header("Látvány")]
    [SerializeField] float spinSpeed = 60f;      // fok / mp
    [SerializeField] float bobHeight = 0.1f;
    [SerializeField] float bobSpeed = 2f;

    Collider col;
    Vector3 startPos;
    bool available = true;

    void Awake()
    {
        col = GetComponent<Collider>();
        startPos = transform.position;
    }

    void Update()
    {
        if (!available) return;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!available) return;

        AmmoInventory inv = other.GetComponentInParent<AmmoInventory>();
        if (inv == null) return;

        int total = 0;
        foreach (var c in contents) total += inv.Add(c.type, c.amount);

        if (total == 0) return;   // mindene tele: a doboz marad

        if (pickupSound) AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        if (respawnTime > 0f)
        {
            SetAvailable(false);
            Invoke(nameof(Respawn), respawnTime);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Respawn() => SetAvailable(true);

    void SetAvailable(bool value)
    {
        available = value;
        col.enabled = value;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = value;
    }
}