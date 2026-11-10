using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    [SerializeField] Weapon[] weapons;   // 1. íj, 2. kard, 3. dobókés, 4. gránát ...
    int current = -1;

    [Tooltip("The character's Animator. WeaponType = slot + 1 (0 = unarmed); a picked up weapon is the slot after the fixed ones.")]
    [SerializeField] Animator animator;

    [Header("Picked up weapon (no inventory yet, one at a time)")]
    [Tooltip("Where a picked up weapon is attached, e.g. the hand_r bone or an empty child of it.")]
    [SerializeField] Transform pickupSocket;
    [Tooltip("Used for the drop direction. If empty, Camera.main.")]
    [SerializeField] Camera playerCamera;
    [SerializeField] float dropForce = 4f;

    static readonly int WeaponTypeHash = Animator.StringToHash("WeaponType");
    static readonly int DrawSpeedHash = Animator.StringToHash("DrawSpeed");

    Weapon pickedWeapon;
    int PickedIndex => weapons.Length;   // its number key comes after the fixed weapons

    public Weapon Current => GetWeapon(current);
    public bool HasPickedWeapon => pickedWeapon != null;

    void Start()
    {
        if (weapons == null) weapons = new Weapon[0];

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null) weapons[i].OnUnequip();
            else Debug.LogWarning("WeaponHolder: weapon slot " + (i + 1) + " is empty (None) in the Weapons list.", this);
        }

        // Starting weapon picked on the character select screen (falls back to the first existing one)
        int start = CharacterSelection.StartingWeaponSlot;
        if (GetWeapon(start) == null || start >= weapons.Length) start = FirstWeaponIndex();
        if (start >= 0) Equip(start);
    }

    int FirstWeaponIndex()
    {
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i] != null) return i;
        return -1;
    }

    void Update()
    {
        // 1-9 gombok: fegyverváltás
        int slots = weapons.Length + (pickedWeapon != null ? 1 : 0);
        for (int i = 0; i < slots && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Equip(i);

        if (Current == null) return;

        Current.SetAiming(Input.GetMouseButton(1));   // jobb klikk = célzás
        Current.HandleInput();
    }

    void Equip(int index)
    {
        Weapon next = GetWeapon(index);
        if (next == null || index == current) return;
        if (Current != null) Current.OnUnequip();
        current = index;
        next.OnEquip();

        if (animator)
        {
            PlayerStats stats = GetComponentInParent<PlayerStats>();
            animator.SetFloat(DrawSpeedHash, stats != null ? stats.DrawSpeedMultiplier : 1f);
            animator.SetInteger(WeaponTypeHash, current + 1);
        }
    }

    Weapon GetWeapon(int index)
    {
        if (index >= 0 && index < weapons.Length) return weapons[index];
        if (index == PickedIndex) return pickedWeapon;
        return null;
    }

    // Called by PlayerInteract when the player picks a weapon up from the world
    public void PickUp(Weapon weapon)
    {
        if (weapon == null || !weapon.CanBePickedUp) return;
        if (pickedWeapon != null) DropPicked();

        pickedWeapon = weapon;
        weapon.OnPickedUp(pickupSocket != null ? pickupSocket : transform);
        Equip(PickedIndex);
    }

    public void DropPicked()
    {
        if (pickedWeapon == null) return;

        if (current == PickedIndex)
        {
            pickedWeapon.SetAiming(false);
            current = -1;
            if (animator) animator.SetInteger(WeaponTypeHash, 0);
        }

        Weapon dropped = pickedWeapon;
        pickedWeapon = null;

        // In front of the camera, so it does not spawn inside the player's capsule
        Transform cam = playerCamera != null ? playerCamera.transform : (Camera.main != null ? Camera.main.transform : transform);
        Vector3 position = cam.position + cam.forward * 0.8f;
        Vector3 velocity = (cam.forward + Vector3.up * 0.3f).normalized * dropForce;
        dropped.OnDropped(position, velocity);
    }
}