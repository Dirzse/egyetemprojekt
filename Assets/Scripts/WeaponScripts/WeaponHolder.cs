using System.Collections.Generic;
using UnityEngine;

// Slots: 1 = fists, 2.. = the character's primary weapons (chosen on the select screen),
// then the weapons below (grenade, knives ...), last = a weapon picked up from the world.
// Number keys or the mouse wheel switch. A picked up weapon is dropped when you switch away from it.
public class WeaponHolder : MonoBehaviour
{
    [Tooltip("Weapons every character has, after the primary weapons (e.g. grenade, throwing knife). " +
             "Do not put the bow / sword here: those are spawned from the character's primary weapons.")]
    [SerializeField] Weapon[] weapons;   // 1. íj, 2. kard, 3. dobókés, 4. gránát ...
    int current = 0;

    [Tooltip("The character's Animator. Found automatically on the player's model if empty (also after the character swap). " +
             "WeaponType comes from the weapon itself (0 = unarmed or no animation, 1 bow, 2 sword, 3 knife, 4 gun).")]
    [SerializeField] Animator animator;

    [Header("Unarmed")]
    [Tooltip("Switches to the fists. Pressing it again draws the last weapon.")]
    [SerializeField] KeyCode holsterKey = KeyCode.X;
    [Tooltip("Fist attacks while no weapon is in the hand. Added automatically if empty.")]
    [SerializeField] UnarmedCombat unarmed;

    [Header("Mouse wheel")]
    [SerializeField] bool scrollSwitch = true;
    [Tooltip("Off: wheel down = next slot.")]
    [SerializeField] bool invertScroll = false;

    [Header("Picked up weapon (no inventory yet, one at a time)")]
    [Tooltip("Where a picked up weapon is attached, e.g. the hand_r bone or an empty child of it.")]
    [SerializeField] Transform pickupSocket;
    [Tooltip("Used for the drop direction. If empty, Camera.main.")]
    [SerializeField] Camera playerCamera;
    [SerializeField] float dropForce = 4f;

    static readonly int WeaponTypeHash = Animator.StringToHash("WeaponType");
    static readonly int DrawSpeedHash = Animator.StringToHash("DrawSpeed");

    readonly List<Weapon> slots = new List<Weapon>();   // [0] = fists (null)
    Weapon pickedWeapon;
    int lastWeapon = -1;
    int primaryCount;
    Animator appliedAnimator;
    int PickedIndex => slots.Count;   // its number key comes after the fixed weapons
    int SlotCount => slots.Count + (pickedWeapon != null ? 1 : 0);

    public Weapon Current => GetWeapon(current);
    public bool HasPickedWeapon => pickedWeapon != null;
    public bool IsUnarmed => Current == null;

    // The Animator of the player's current model. The model swap replaces the Animator, so it is looked up again when needed.
    public Animator CharacterAnimator
    {
        get
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                PlayerMovement player = GetComponentInParent<PlayerMovement>();
                Transform root = player != null ? player.transform : transform.root;
                animator = null;
                foreach (Animator found in root.GetComponentsInChildren<Animator>())
                {
                    if (found.transform.IsChildOf(transform)) continue;   // weapon models can have an Animator too
                    if (found.isHuman) { animator = found; break; }
                    if (animator == null) animator = found;
                }
            }
            return animator;
        }
    }

    void Start()
    {
        if (unarmed == null) unarmed = GetComponent<UnarmedCombat>();
        if (unarmed == null) unarmed = gameObject.AddComponent<UnarmedCombat>();

        slots.Add(null);   // fists

        foreach (PrimaryWeaponData data in GetPrimaryWeapons())
        {
            Weapon spawned = SpawnPrimary(data);
            if (spawned == null) continue;
            slots.Add(spawned);
            primaryCount++;
        }

        if (weapons == null) weapons = new Weapon[0];
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null) slots.Add(weapons[i]);
            else Debug.LogWarning("WeaponHolder: weapon slot " + (i + 1) + " is empty (None) in the Weapons list.", this);
        }

        for (int i = 1; i < slots.Count; i++) slots[i].OnUnequip();

        // Fists by default, the first primary weapon if the character has one
        current = 0;
        if (primaryCount > 0) Equip(1);
        else UpdateAnimator();
    }

    // Chosen on the select screen, or the character's default weapons when the game scene is started directly
    PrimaryWeaponData[] GetPrimaryWeapons()
    {
        if (CharacterSelection.Primaries != null) return CharacterSelection.Primaries;

        PlayerStats stats = GetComponentInParent<PlayerStats>();
        if (stats != null && stats.Data != null && stats.Data.defaultWeapons != null) return stats.Data.defaultWeapons;
        return new PrimaryWeaponData[0];
    }

    Weapon SpawnPrimary(PrimaryWeaponData data)
    {
        if (data == null) return null;
        if (data.weaponPrefab == null)
        {
            Debug.LogWarning("WeaponHolder: '" + data.name + "' has no Weapon Prefab.", data);
            return null;
        }

        GameObject spawned = Instantiate(data.weaponPrefab, transform);   // keeps the prefab's local position / rotation
        spawned.name = data.displayName;
        Weapon weapon = spawned.GetComponent<Weapon>();
        if (weapon == null)
        {
            Debug.LogWarning("WeaponHolder: the prefab of '" + data.name + "' has no Weapon component on its root.", data);
            Destroy(spawned);
        }
        return weapon;
    }

    void Update()
    {
        // 1-9 gombok: fegyverváltás
        int count = SlotCount;
        for (int i = 0; i < count && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Equip(i);

        if (scrollSwitch)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int step = (scroll < 0f) != invertScroll ? 1 : -1;
                Equip((current + step + count) % count);
            }
        }

        if (Input.GetKeyDown(holsterKey))
        {
            if (current != 0) Equip(0);
            else Equip(lastWeapon > 0 && GetWeapon(lastWeapon) != null ? lastWeapon : 1);
        }

        UpdateAnimator();

        if (Current == null)
        {
            if (unarmed != null) unarmed.HandleInput();
            return;
        }

        Current.SetAiming(Input.GetMouseButton(1));   // jobb klikk = célzás
        Current.HandleInput();
    }

    // Sent every frame: a swapped model gets a new Animator with default parameters
    void UpdateAnimator()
    {
        Animator anim = CharacterAnimator;
        if (anim == null || anim.runtimeAnimatorController == null) return;

        if (anim != appliedAnimator)
        {
            appliedAnimator = anim;
            PlayerStats stats = GetComponentInParent<PlayerStats>();
            anim.SetFloat(DrawSpeedHash, stats != null ? stats.DrawSpeedMultiplier : 1f);
        }
        anim.SetInteger(WeaponTypeHash, Current != null ? Current.AnimationType : 0);
    }

    void Equip(int index)
    {
        if (index < 0 || index >= SlotCount || index == current) return;
        Weapon next = GetWeapon(index);
        if (index != 0 && next == null) return;

        // A picked up weapon can not be kept: switching away drops it
        if (current == PickedIndex && pickedWeapon != null) DropPickedWeapon();
        else if (Current != null) Current.OnUnequip();

        current = index;
        if (index != 0 && index != PickedIndex) lastWeapon = index;
        if (unarmed != null) unarmed.Cancel();
        if (next != null) next.OnEquip();

        Animator anim = CharacterAnimator;
        if (anim)
        {
            PlayerStats stats = GetComponentInParent<PlayerStats>();
            anim.SetFloat(DrawSpeedHash, stats != null ? stats.DrawSpeedMultiplier : 1f);
            anim.SetInteger(WeaponTypeHash, next != null ? next.AnimationType : 0);
        }
    }

    public void Holster() => Equip(0);

    Weapon GetWeapon(int index)
    {
        if (index >= 0 && index < slots.Count) return slots[index];
        if (index == PickedIndex) return pickedWeapon;
        return null;
    }

    // Called by PlayerInteract when the player picks a weapon up from the world
    public void PickUp(Weapon weapon)
    {
        if (weapon == null || !weapon.CanBePickedUp) return;
        if (pickedWeapon != null) DropPickedWeapon();   // only one at a time

        pickedWeapon = weapon;
        weapon.OnPickedUp(pickupSocket != null ? pickupSocket : transform);
        Equip(PickedIndex);   // in hand right away
    }

    // G key (PlayerInteract): drops it and goes back to the last own weapon (or the fists)
    public void DropPicked()
    {
        if (pickedWeapon == null) return;
        bool wasInHand = current == PickedIndex;
        DropPickedWeapon();
        if (wasInHand) Equip(lastWeapon > 0 && GetWeapon(lastWeapon) != null ? lastWeapon : 0);
    }

    void DropPickedWeapon()
    {
        if (current == PickedIndex)
        {
            pickedWeapon.OnUnequip();
            current = 0;
            UpdateAnimator();
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
