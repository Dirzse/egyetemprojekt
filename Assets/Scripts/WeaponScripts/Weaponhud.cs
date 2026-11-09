using TMPro;
using UnityEngine;

// Kiírja az aktív fegyver nevét és a hozzá tartozó lőszert.
public class WeaponHUD : MonoBehaviour
{
    [SerializeField] WeaponHolder holder;
    [SerializeField] AmmoInventory inventory;     // ha üres, a holder szülői közül keresi
    [SerializeField] TMP_Text weaponNameText;
    [SerializeField] TMP_Text ammoText;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color emptyColor = new Color(1f, 0.3f, 0.3f);

    Weapon shown;

    void Awake()
    {
        if (!inventory && holder) inventory = holder.GetComponentInParent<AmmoInventory>(true);
    }

    void OnEnable()  { if (inventory) inventory.Changed += OnAmmoChanged; }
    void OnDisable() { if (inventory) inventory.Changed -= OnAmmoChanged; }

    void Update()
    {
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
            weaponNameText.text = "";
            ammoText.text = "";
            return;
        }

        weaponNameText.text = shown.DisplayName;

        // Lőszer nélküli fegyver (kard): nincs kiírás
        if (!shown.TryGetAmmoType(out AmmoType type))
        {
            ammoText.text = "";
            return;
        }

        // Nincs AmmoInventory: végtelen lőszer
        if (inventory == null)
        {
            ammoText.text = "∞";
            ammoText.color = normalColor;
            return;
        }

        int amount = inventory.Get(type);
        ammoText.text = amount.ToString();
        ammoText.color = amount > 0 ? normalColor : emptyColor;
    }
}