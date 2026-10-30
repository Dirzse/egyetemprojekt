using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class CharacterHealth : MonoBehaviour
{
    [Header("Életerő beállítások")]
    public float maxHealth = 100f;
    private float currentHealth;

    // A zóna ezen keresztül olvassa ki az aktuális életerőt (csak olvasni lehet, átírni nem)
    public float CurrentHealth => currentHealth;

    [Header("UI beállítások")]
    public Slider healthSlider; // A piros, azonnal csökkenő csúszka
    public Slider easeHealthSlider; // A sárga, lassan követő csúszka

    [Header("Sebesség beállítások")]
    public float lerpSpeed = 5f; // Milyen gyorsan kövesse le a sárga csúszka a sebzést

    [Header("Események")]
    public UnityEvent onDeath; // Ebbe az eseménybe köthető be a specifikus halál logika

    [Header("Automatikus gyógyulás")]
    public bool autoRegenEnabled = true; // Ha be van pipálva, működik az automatikus gyógyulás (kipipálva semmi nem történik)
    public float regenDelay = 60f;   // Sebzés után ennyi másodperc múlva kezd el gyógyulni
    public float regenSpeed = 0.05f; // A max életerő ekkora része gyógyul másodpercenként (0.05 = 5% / mp)
    public float regenTimeLeft;      // Mennyi idő van még a gyógyulás kezdetéig (játék közben itt figyelheted)

    public float otherHealPenalty = 25f; // Ha nem az auto heal gyógyít (potion, zóna, E gomb), utána ennyi mp-et kell várni

    private bool regenPending;       // Kapott-e sebzést, amiből még nem gyógyult fel
    private bool isRegenHealing;     // Épp az auto heal gyógyít-e (így az AddHealth meg tudja különböztetni)

    private Collider ownCollider; // A karakter saját collidere (ezen az objektumon vagy egy gyerekén)

    private void Start()
    {
        // Kezdeti életerő beállítása a maximumra induláskor
        currentHealth = maxHealth;

        // Beállítjuk a sárga csúszka maximumát induláskor
        if (easeHealthSlider != null)
        {
            easeHealthSlider.maxValue = maxHealth;
            easeHealthSlider.value = maxHealth;
        }

        // Megkeressük a karakter saját colliderét az érintés-ellenőrzéshez
        // Először ezen az objektumon, aztán a gyerekein keressük
        ownCollider = GetComponent<Collider>();
        if (ownCollider == null) ownCollider = GetComponentInChildren<Collider>();
        if (ownCollider == null)
        {
            Debug.LogWarning("CharacterHealth: nincs Collider a karakteren, a HealthAndDamageZone-ok nem fognak működni.");
        }

        UpdateHealthUI();
    }

    private void Update()
    {
        // Finom lekövetés (Easing) a sárga sávnál a lerp függvénnyel
        if (easeHealthSlider != null && easeHealthSlider.value != currentHealth)
        {
            // A lerp matematikai átmenetet képez a jelenlegi sárga érték és az új, csökkentett health érték között
            easeHealthSlider.value = Mathf.Lerp(easeHealthSlider.value, currentHealth, lerpSpeed * Time.deltaTime);

            // Ha már elég közel van, ráillesztjük, így a következő frame-ektől nem számol tovább
            if (Mathf.Abs(easeHealthSlider.value - currentHealth) < 0.01f)
            {
                easeHealthSlider.value = currentHealth;
            }
        }

        // Automatikus gyógyulás (a billentyűzet-ellenőrzés elé kell, mert az ott lévő return kihagyná)
        HandleRegen();

        if (Keyboard.current == null) return;
        // wasPressedThisFrame = csak egyszer fut le, amikor lenyomod (nem folyamatosan, amíg nyomva tartod)
        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("E lenyomva");
            AddHealth(5);
        }
        else if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log("T lenyomva");
            TakeDamage(5);
        }
    }

    // Sebzés után regenDelay mp-et vár, utána lassan gyógyít, amíg tele nem lesz
    private void HandleRegen()
    {
        if (!regenPending) return;

        // Ha ki van kapcsolva, nem számol vissza és nem gyógyít
        // (a sebzést megjegyzi, így visszakapcsoláskor onnan folytatja, ahol abbahagyta)
        if (!autoRegenEnabled) return;

        // Ha közben tele lett (pl. potionnal vagy gyógyító zónával), megszűnik
        if (currentHealth >= maxHealth)
        {
            regenPending = false;
            regenTimeLeft = 0f;
            return;
        }

        // Először megvárjuk a késleltetést
        if (regenTimeLeft > 0f)
        {
            regenTimeLeft -= Time.deltaTime;
            return;
        }

        // Lassú gyógyulás: maxHealth * regenSpeed másodpercenként
        isRegenHealing = true;
        AddHealth(maxHealth * regenSpeed * Time.deltaTime);
        isRegenHealing = false;

        // Ha elérte a maximumot, megszűnik
        if (currentHealth >= maxHealth)
        {
            regenPending = false;
        }
    }

    // A zóna ezzel ellenőrzi, hogy a talált collider tényleg a karakteré-e
    public bool HasCollider(Collider other)
    {
        return ownCollider != null && other == ownCollider;
    }

    // 1. Metódus: Életerő növelése
    public void AddHealth(float healAmount)
    {
        // Ha a karakter már halott, megakadályozzuk a gyógyulást
        if (currentHealth <= 0) return;

        currentHealth += healAmount;

        // Az életerő nem lépheti túl a megadott maximumot
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // Büntetés: ha nem az auto heal gyógyított, az auto heal csak otherHealPenalty mp múlva folytatódik
        // (ha a sebzés miatti várakozásból több van hátra, azt nem rövidítjük le)
        // Csak akkor kell várni, ha a HP még nem full (ha feltöltötte, nincs mit gyógyítani)
        if (!isRegenHealing && regenPending && currentHealth < maxHealth)
        {
            regenTimeLeft = Mathf.Max(regenTimeLeft, otherHealPenalty);
        }

        UpdateHealthUI();
    }

    // 2. Metódus: Sebzés elszenvedése és életerő csökkentése
    public void TakeDamage(float damageAmount)
    {
        // Védelem, hogy ne hívódjon meg a sebzés és a halál többször, ha már 0-n van
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;

        if (currentHealth > 0)
        {
            // Ha a karakternek van még életereje, szinkronizáljuk a UI-t
            UpdateHealthUI();

            // Sebzés után elölről indul a gyógyulás visszaszámlálása (gyógyulás közben is megszakítja)
            regenPending = true;
            regenTimeLeft = regenDelay;
        }
        else
        {
            // Ha az életerő 0 vagy kevesebb, pontosan 0-ra állítjuk, frissítjük a UI-t, majd meghívjuk a halált
            currentHealth = 0;
            UpdateHealthUI();
            Die();
        }
    }

    // 3. Metódus: Életerő UI (Slider) frissítése
    public void UpdateHealthUI()
    {
        // Csak akkor frissítünk, ha a csúszka (Slider) be van állítva az Inspectorban
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth; // Azonnali frissítés a piros sávnál
        }
    }

    // 4. Metódus: Halál kezelése és a pálya (map) resetelése
    public void Die()
    {
        // Meghívjuk az eseményt is, hátha rá van kötve más logika (pl. hang lejátszása)
        if (onDeath != null)
        {
            onDeath.Invoke();
        }

        // Lekérdezzük a jelenleg aktív jelenetet (pályát), és újra betöltjük azt
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}