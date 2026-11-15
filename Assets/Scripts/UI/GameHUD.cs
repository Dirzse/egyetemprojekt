using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// In-game HUD in the blue style of the character select screen.
// Top left: character name and health. Bottom right: a tile for every weapon slot (icon, count, key);
// the slot in hand grows a bit and its name shows above it.
// The UI itself is a normal object in the scene: make it with Tools > UI > Build Game HUD, then move, resize
// and recolor anything in the editor. This script only fills in the values and finds the player by itself.
public class GameHUD : MonoBehaviour
{
    [Header("Player (found automatically if empty)")]
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] CharacterHealth health;
    [SerializeField] AmmoInventory inventory;

    [Header("Health")]
    [SerializeField] TMP_Text characterName;
    [SerializeField] TMP_Text healthValue;
    [SerializeField] Image healthFill;
    [SerializeField] RectTransform healthTrail;

    [Header("Weapons")]
    [Tooltip("Parent of the tiles. Its right bottom corner is where the row ends.")]
    [SerializeField] RectTransform tileRow;
    [Tooltip("Empty = the HUDSlot children of the tile row, in order. Extra tiles are hidden.")]
    [SerializeField] List<HUDSlot> slots = new List<HUDSlot>();
    [SerializeField] RectTransform weaponNameRect;
    [SerializeField] TMP_Text weaponName;
    [Tooltip("Background of the tile in hand (a left to right gradient).")]
    [SerializeField] Sprite selectedBackground;

    [Header("Icons (empty = the built-in ones from Resources/HUDIcons)")]
    [SerializeField] Sprite fistIcon;
    [SerializeField] Sprite bowIcon;
    [SerializeField] Sprite swordIcon;
    [SerializeField] Sprite knifeIcon;
    [SerializeField] Sprite grenadeIcon;
    [SerializeField] Sprite gunIcon;
    [Tooltip("Picked up weapons and anything else without its own icon.")]
    [SerializeField] Sprite otherIcon;

    [Header("Colors (set at runtime)")]
    [SerializeField] Color accent = new Color(0.33f, 0.70f, 0.86f);
    [SerializeField] Color tileColor = new Color(0.04f, 0.08f, 0.12f, 0.78f);
    [SerializeField] Color textColor = new Color(0.9f, 0.95f, 1f);
    [SerializeField] Color dimText = new Color(0.55f, 0.65f, 0.74f);
    [SerializeField] Color keyColor = new Color(0.82f, 0.88f, 0.93f);
    [SerializeField] Color lowColor = new Color(0.85f, 0.38f, 0.33f);

    [Header("Behaviour")]
    [SerializeField] float gap = 6f;
    [Tooltip("Size of the tile in hand compared to the others (1.25 = 25% bigger).")]
    [SerializeField] float selectedScale = 1.25f;
    [Tooltip("How fast the tiles grow and shrink when switching.")]
    [SerializeField] float scaleSpeed = 12f;
    [Tooltip("Extra size of the icon in the tile in hand (1.15 = 15% bigger), on top of the tile's own growth.")]
    [SerializeField] float selectedIconScale = 1.15f;
    [Tooltip("Health at or below this part of the maximum turns the bar red.")]
    [SerializeField] float lowHealthPart = 0.25f;

    readonly Dictionary<string, Sprite> builtInIcons = new Dictionary<string, Sprite>();
    float trailPart = 1f;
    bool nameSet;
    bool warnedSlots;

    void Awake()
    {
        if (slots.Count == 0 && tileRow != null) slots.AddRange(tileRow.GetComponentsInChildren<HUDSlot>(true));
    }

    void Update()
    {
        FindPlayer();
        UpdateHealth();
        UpdateWeapons();
    }

    void FindPlayer()
    {
        if (weaponHolder == null) weaponHolder = FindFirstObjectByType<WeaponHolder>();
        if (weaponHolder == null) return;
        if (health == null) health = weaponHolder.GetComponentInParent<CharacterHealth>();
        if (inventory == null) inventory = weaponHolder.GetComponentInParent<AmmoInventory>();

        if (!nameSet && characterName != null)
        {
            PlayerStats stats = weaponHolder.GetComponentInParent<PlayerStats>();
            if (stats != null && stats.Data != null)
            {
                characterName.text = stats.Data.characterName;
                nameSet = true;
            }
        }
    }

    // ---------------------------------------------------------------- health

    void UpdateHealth()
    {
        if (health == null || healthFill == null) return;

        float max = Mathf.Max(1f, health.maxHealth);
        float part = Mathf.Clamp01(health.CurrentHealth / max);
        trailPart = part > trailPart ? part : Mathf.MoveTowards(trailPart, part, Time.deltaTime * 0.35f);

        healthFill.rectTransform.anchorMax = new Vector2(part, 1f);
        if (healthTrail != null) healthTrail.anchorMax = new Vector2(trailPart, 1f);
        healthFill.color = part <= lowHealthPart ? lowColor : accent;
        if (healthValue != null) healthValue.text = Mathf.CeilToInt(health.CurrentHealth) + " / " + Mathf.RoundToInt(max);
    }

    // ---------------------------------------------------------------- weapons

    void UpdateWeapons()
    {
        if (weaponHolder == null || slots.Count == 0) return;

        int count = weaponHolder.SlotCount;
        if (count > slots.Count && !warnedSlots)
        {
            warnedSlots = true;
            Debug.LogWarning("GameHUD: the player has " + count + " weapon slots but the HUD only " + slots.Count +
                             " tiles. Duplicate a tile under '" + (tileRow != null ? tileRow.name : "the tile row") + "'.", this);
        }

        int current = weaponHolder.CurrentSlot;
        int shown = Mathf.Min(count, slots.Count);

        // The tile in hand grows, the others go back; the row stays on its right edge
        float total = 0f;
        for (int i = 0; i < shown; i++) total += slots[i].Rect.sizeDelta.x * slots[i].scale;
        total += Mathf.Max(0, shown - 1) * gap;
        float x = -total;

        for (int i = 0; i < slots.Count; i++)
        {
            HUDSlot slot = slots[i];
            bool visible = i < shown;
            if (slot.gameObject.activeSelf != visible) slot.gameObject.SetActive(visible);
            if (!visible) continue;

            Weapon weapon = weaponHolder.GetSlot(i);
            bool selected = i == current;

            float target = selected ? selectedScale : 1f;
            slot.scale = Mathf.MoveTowards(slot.scale, target, scaleSpeed * Time.deltaTime * Mathf.Max(0.01f, selectedScale - 1f));
            slot.Rect.localScale = new Vector3(slot.scale, slot.scale, 1f);
            slot.Rect.anchoredPosition = new Vector2(x, 0f);

            float width = slot.Rect.sizeDelta.x * slot.scale;
            if (selected && weaponNameRect != null)
                weaponNameRect.anchoredPosition = new Vector2(x + width * 0.5f, slot.Rect.sizeDelta.y * slot.scale + 6f);
            x += width + gap;

            if (slot.background != null)
            {
                slot.background.sprite = selected ? selectedBackground : null;
                slot.background.color = selected && selectedBackground != null ? Color.white : tileColor;
            }
            if (slot.underline != null) slot.underline.enabled = selected;
            if (slot.key != null) slot.key.color = selected ? accent : keyColor;

            if (slot.icon != null)
            {
                slot.icon.sprite = IconFor(i, weapon);
                slot.icon.color = selected ? textColor : dimText;
                float iconTarget = selected ? selectedIconScale : 1f;
                float iconScale = Mathf.MoveTowards(slot.icon.rectTransform.localScale.x, iconTarget,
                                                    scaleSpeed * Time.deltaTime * Mathf.Max(0.01f, selectedIconScale - 1f));
                slot.icon.rectTransform.localScale = new Vector3(iconScale, iconScale, 1f);
            }

            if (slot.count != null)
            {
                int ammo = AmmoOf(weapon, out bool usesAmmo);
                slot.count.text = usesAmmo ? ammo.ToString() : "";
                slot.count.color = usesAmmo && ammo <= 0 ? lowColor : textColor;
            }
        }

        if (weaponName != null) weaponName.text = WeaponLabel(weaponHolder.GetSlot(current));
    }

    // The knife can be thrown or used in the hand, so it is just "Knife"
    static string WeaponLabel(Weapon weapon)
    {
        if (weapon == null) return "Fists";
        if (weapon is ThrowingKnife) return "Knife";
        return weapon.DisplayName;
    }

    Sprite IconFor(int slot, Weapon weapon)
    {
        if (slot == 0 || weapon == null) return Pick(fistIcon, "fist");
        if (weapon is GrenadeThrower) return Pick(grenadeIcon, "grenade");
        switch (weapon.AnimationType)
        {
            case 1: return Pick(bowIcon, "bow");
            case 2: return Pick(swordIcon, "sword");
            case 3: return Pick(knifeIcon, "knife");
            case 4: return Pick(gunIcon, "gun");
            default: return Pick(otherIcon, "weapon");
        }
    }

    Sprite Pick(Sprite assigned, string builtInName)
    {
        if (assigned != null) return assigned;
        if (builtInIcons.TryGetValue(builtInName, out Sprite cached)) return cached;

        // Works whether the PNG was imported as a Sprite or as a plain texture
        Sprite sprite = Resources.Load<Sprite>("HUDIcons/" + builtInName);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>("HUDIcons/" + builtInName);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        builtInIcons[builtInName] = sprite;
        return sprite;
    }

    int AmmoOf(Weapon weapon, out bool usesAmmo)
    {
        usesAmmo = false;
        if (weapon == null) return 0;

        if (weapon is ThrowingKnife knife)
        {
            usesAmmo = true;
            return knife.KnifeCount;
        }
        if (!weapon.TryGetAmmoType(out AmmoType type)) return 0;
        if (inventory == null) return 0;   // no inventory: unlimited, nothing to show
        usesAmmo = true;
        return inventory.Get(type);
    }
}
