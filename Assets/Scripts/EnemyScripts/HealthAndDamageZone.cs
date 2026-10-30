using UnityEngine;
using System.Collections.Generic;

// Arra az objektumra tedd, ami sebez vagy gyógyít, ha hozzáérnek.
// Csak egy Collider kell rá, Rigidbody nem. Az érzékelést most a zóna maga végzi,
// és ő szól a CharacterHealth-nek, hogy sebződjön vagy gyógyuljon.
public class HealthAndDamageZone : MonoBehaviour
{
    public enum ZoneType { None, Damage, Heal }

    [Header("Zóna típusa")]
    public ZoneType type = ZoneType.Damage; // Damage = sebez, Heal = gyógyít, None = nem csinál semmit

    [Header("Értékek")]
    public float amountOnTouch = 5f; // Azonnali hatás, amikor hozzáér
    public float amountPerTick = 5f; // Ennyit sebez/gyógyít minden "tick"-nél, amíg hozzáér
    public float tickInterval = 1f;  // Hány másodpercenként

    [Header("Készlet")]
    public bool infinite = true;     // Ha be van pipálva, sosem fogy ki
    public float maxAmount = 300f;   // Ennyi sebzés/gyógyítás van benne összesen (ha nem infinite)
    public float remainingAmount;    // Mennyi van még hátra (játék közben itt figyelheted)
    public bool onlyConsumeUsed = true; // Bepipálva: csak a ténylegesen felhasznált mennyiség fogy (pl. teli HP-nál nem fogy).
                                        // Kipipálva: mindig a teljes kiosztott mennyiség fogy.

    [Header("Újratöltődés")]
    public bool canRefill = false;   // Ha be van pipálva, kifogyás után újratöltődik
    public float refillDelay = 25f;  // Kifogyás után ennyi másodperc múlva töltődik újra
                                     // (pontosabban: az utolsó használat után ennyi mp múlva kezd el tölteni)
    public float refillSpeed = 5f;   // Másodpercenként ennyivel töltődik vissza (refillSpeed * Time.deltaTime frame-enként)
    public float refillTimeLeft;     // Mennyi idő van még az újratöltésig (játék közben itt figyelheted)
    public bool touchInterruptsRefill = false; // Bepipálva: már a puszta érintés is megszakítja/újraindítja a várakozást,
                                               // akkor is, ha nem fogyott semmi (pl. teli HP-val állsz a gyógyító zónán)

    [Header("Érintés érzékelés (Rigidbody nélkül)")]
    [Tooltip("Ennyivel nagyobb dobozban keres, hogy a puszta érintést is észrevegye.\n" +
             "Is Trigger BEPIPÁLVA (nem fizikális, át lehet menni rajta): 0\n" +
             "Is Trigger KIPIPÁLVA (fizikális, szilárd tárgy, pl. kaktusz): 0.2")]
    public float touchPadding = 0.2f; // Ennyivel nagyobb dobozban keresünk, hogy a puszta érintést is észrevegye
                                      // Trigger (nem fizikális) objektumnál: 0, fizikális (szilárd) objektumnál: 0.2
    public bool debugZone = false;     // Bepipálva kiírja a Console-ba, ki ért hozzá a zónához
    public LayerMask targetLayers = ~0; // Csak ezeken a rétegeken keres (pl. csak "Player"); alapból mindenen (Everything)

    private ZoneType originalType;   // Az eredeti típus, hogy újratöltéskor vissza tudjuk állítani
    private bool depleted;           // Kifogyott-e (így a kézzel None-ra állított zóna nem töltődik újra)
    private Collider zoneCollider;   // A zóna saját collidere
    private Dictionary<CharacterHealth, float> activeTargets = new Dictionary<CharacterHealth, float>(); // Akik épp hozzáérnek + időzítőjük

    // Újrahasznosított tárolók, hogy ne keletkezzen szemét (GC) minden frame-ben
    private Collider[] hitBuffer = new Collider[16];                      // Ide kerülnek a talált colliderek (max 16 egyszerre)
    private HashSet<CharacterHealth> touched = new HashSet<CharacterHealth>(); // Akik ebben a frame-ben hozzáérnek
    private List<CharacterHealth> toRemove = new List<CharacterHealth>();      // Akiket ki kell venni a listából

    // Az Inspectorban minden módosításkor lefut: végtelen zónánál nincs értelme az újratöltésnek
    private void OnValidate()
    {
        if (infinite) canRefill = false;
    }

    private void Awake()
    {
        // Induláskor tele a készlet
        remainingAmount = maxAmount;
        originalType = type;

        // A zóna saját collidere, ezzel nézzük meg, ki ér hozzá
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            Debug.LogWarning("HealthAndDamageZone: nincs Collider a(z) " + name + " objektumon, a zóna nem fog működni.");
        }
    }

    private void Update()
    {
        // Először megnézzük, ki ér hozzá, és kiosztjuk a sebzést/gyógyítást
        CheckTouch();

        // Utána jön az újratöltés
        Refill();
    }

    // Megnézi, milyen karakterek érnek hozzá a zónához, és alkalmazza rájuk a hatást.
    // Azért kell kézzel, mert a Unity OnTrigger/OnCollision eventjei Rigidbody nélkül nem futnak le.
    private void CheckTouch()
    {
        if (zoneCollider == null) return;

        // Lekérdezzük az összes collidert, ami a zóna körüli dobozba belelóg (triggereket is)
        Bounds b = zoneCollider.bounds;
        // NonAlloc: a meglévő tömbbe tölti a találatokat, nem hoz létre újat (csak a targetLayers rétegeken keres)
        int hitCount = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * touchPadding, hitBuffer, Quaternion.identity, targetLayers, QueryTriggerInteraction.Collide);

        // Ha senki nincs a közelben és senkit sem kezelünk, nincs több dolgunk
        if (hitCount == 0 && activeTargets.Count == 0) return;

        // Összegyűjtjük, mely karakterek érnek most hozzá
        touched.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];

            // A saját (és gyerek) collidereinket kihagyjuk
            if (hit == zoneCollider || hit.transform.IsChildOf(transform)) continue;

            CharacterHealth health = hit.GetComponentInParent<CharacterHealth>();
            if (health != null && health.HasCollider(hit)) touched.Add(health);
        }

        // Azokat, akik már nem érnek hozzá, kivesszük a listából
        toRemove.Clear();
        foreach (CharacterHealth health in activeTargets.Keys)
        {
            if (health == null || !touched.Contains(health)) toRemove.Add(health);
        }
        foreach (CharacterHealth health in toRemove)
        {
            activeTargets.Remove(health);
        }

        // Új érintésnél azonnali hatás, utána időzített hatás
        foreach (CharacterHealth health in touched)
        {
            // Ha a puszta érintés is megszakítja a töltést, újraindítjuk a várakozást
            NotifyTouch();

            if (!activeTargets.ContainsKey(health))
            {
                activeTargets[health] = 0f;
                if (debugZone) Debug.Log(health.name + " hozzáért a zónához: " + name + " (típus: " + type + ")");
                ApplyTo(health, amountOnTouch);
            }
            else
            {
                float timer = activeTargets[health] + Time.deltaTime;
                float interval = Mathf.Max(0.01f, tickInterval);
                if (timer >= interval)
                {
                    timer -= interval;
                    ApplyTo(health, amountPerTick);
                }
                activeTargets[health] = timer;
            }
        }
    }

    // A zóna típusa alapján szól a karakternek, hogy sebződjön vagy gyógyuljon
    private void ApplyTo(CharacterHealth health, float amount)
    {
        // Mennyit tud adni a zóna (None vagy kifogyott zónánál 0)
        float available = GetAvailable(amount);
        if (available <= 0f) return;

        // Megjegyezzük az életerőt, hogy csak a ténylegesen felhasznált mennyiséget vonjuk le a zónából
        // (pl. teli életerőnél a gyógyító zóna nem fogy)
        float before = health.CurrentHealth;

        if (type == ZoneType.Damage)
        {
            health.TakeDamage(available);
        }
        else if (type == ZoneType.Heal)
        {
            health.AddHealth(available);
        }

        // onlyConsumeUsed alapján a ténylegeset vagy a teljes kiosztottat vonjuk le
        Consume(Mathf.Abs(health.CurrentHealth - before), available);
    }

    private void Refill()
    {
        // Csak akkor számolunk vissza, ha kifogyott és újratölthető
        // (most már akkor is, ha csak részben fogyott el, és nem végtelen)
        if (!canRefill || infinite) return;

        // Tele van, nincs mit tölteni
        if (remainingAmount >= maxAmount) return;

        // Először megvárjuk a késleltetést
        if (refillTimeLeft > 0f)
        {
            refillTimeLeft -= Time.deltaTime;
            return;
        }

        // Újratöltés: lassan, fokozatosan töltjük vissza a készletet
        refillTimeLeft = 0f;
        remainingAmount = Mathf.Min(maxAmount, remainingAmount + refillSpeed * Time.deltaTime);

        // Ha kifogyott volt, visszaáll az eredeti típus, amint van benne valami
        if (depleted && remainingAmount > 0f)
        {
            type = originalType;
            depleted = false;
        }
    }

    // Megmondja, a kért mennyiségből mennyit tud ténylegesen adni a zóna
    public float GetAvailable(float requested)
    {
        if (type == ZoneType.None) return 0f;
        if (infinite) return requested;
        return Mathf.Min(requested, remainingAmount);
    }

    // Levonja a felhasznált mennyiséget; ha kifogy, None lesz a zóna
    // used = amennyi ténylegesen hatott az életerőre, offered = amennyit a zóna kiosztott
    public void Consume(float used, float offered)
    {
        if (infinite) return;

        float amount = onlyConsumeUsed ? used : offered;
        if (amount <= 0f) return; // Nem fogyott semmi (pl. teli HP-nál), ez nem számít használatnak

        remainingAmount -= amount;

        // Használat után (akkor is, ha épp töltődött = félbeszakította) újra ki kell várni a késleltetést
        refillTimeLeft = refillDelay;

        if (remainingAmount <= 0f)
        {
            remainingAmount = 0f;
            type = ZoneType.None;
            depleted = true;
        }
    }

    // Minden frame-ben meghívódik, amíg a karakter hozzáér a zónához
    public void NotifyTouch()
    {
        // Ha a puszta érintés is megszakítja a töltést, újraindítjuk a várakozást
        if (touchInterruptsRefill && !infinite && remainingAmount < maxAmount)
        {
            refillTimeLeft = refillDelay;
        }
    }
}