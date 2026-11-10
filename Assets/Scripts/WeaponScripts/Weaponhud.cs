using TMPro;
using UnityEngine;

// Kiírja az aktív fegyver nevét és a hozzá tartozó lőszert.
public class WeaponHUD : MonoBehaviour
{
    [SerializeField] WeaponHolder holder;         // if empty, found in the scene
    [SerializeField] AmmoInventory inventory;     // ha üres, a holder szülői közül keresi
    [SerializeField] TMP_Text weaponNameText;
    [SerializeField] TMP_Text ammoText;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color emptyColor = new Color(1f, 0.3f, 0.3f);

    Weapon shown;

    void Awake()
    {
        if (!holder) holder = FindFirstObjectByType<WeaponHolder>();
        if (!holder) Debug.LogWarning("WeaponHUD: no WeaponHolder found.", this);
        if (!inventory && holder) inventory = holder.GetComponentInParent<AmmoInventory>(true);
    }

    void OnEnable()  { if (inventory) inventory.Changed += OnAmmoChanged; }
    void OnDisable() { if (inventory) inventory.Changed -= OnAmmoChanged; }

    void Update()
    {
        if (!holder) return;

        // Fegyverváltás figyelése
        if (holder.Current != shown)
        {
            shown = holder.Current;
            Refresh();
        }
    }

    void OnAmmoChanged(AmmoType type, int amount) => Refresh();

    void Refresh()
    {
        if (shown == null)
        {
            SetText(weaponNameText, "");
            SetText(ammoText, "");
            return;
        }

        SetText(weaponNameText, shown.DisplayName);

        // Lőszer nélküli fegyver (kard): nincs kiírás
        if (!shown.TryGetAmmoType(out AmmoType type))
        {
            SetText(ammoText, "");
            return;
        }

        // Nincs AmmoInventory: végtelen lőszer
        if (inventory == null)
        {
            SetText(ammoText, "∞");
            if (ammoText) ammoText.color = normalColor;
            return;
        }

        int amount = inventory.Get(type);
        SetText(ammoText, amount.ToString());
        if (ammoText) ammoText.color = amount > 0 ? normalColor : emptyColor;
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text) text.text = value;
    }
}