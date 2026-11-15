using UnityEngine;

// Weapon kinds a character can be good with. A character's primary slot can allow several (e.g. Sword | Bow).
[System.Flags]
public enum WeaponCategory
{
    None = 0,
    Bow = 1 << 0,
    Sword = 1 << 1,
    Gun = 1 << 2,
    Spear = 1 << 3,
    Axe = 1 << 4,
}

// One selectable primary weapon (e.g. "Hunting Bow", "Long Bow"). Create with: right click > Create > Game > Primary Weapon
[CreateAssetMenu(fileName = "NewPrimaryWeapon", menuName = "Game/Primary Weapon")]
public class PrimaryWeaponData : ScriptableObject
{
    public string displayName = "New Weapon";
    [TextArea] public string description;

    [Tooltip("Which kind of weapon this is (pick one). Characters whose slot allows this kind can choose it.")]
    public WeaponCategory category = WeaponCategory.Sword;

    [Tooltip("Prefab with a Weapon component (Bow, Sword ...) on its root. It is spawned under the WeaponHolder, " +
             "keeping its local position / rotation, so make it from a weapon that is already placed under the WeaponHolder.")]
    public GameObject weaponPrefab;

    [Tooltip("Model shown on the character select screen. Empty = the weapon prefab is shown.")]
    public GameObject previewModel;

    public GameObject PreviewModel => previewModel != null ? previewModel : weaponPrefab;

    public bool Fits(WeaponCategory allowed) => (allowed & category) != 0;
}
