using UnityEngine;

// Look at something and press F to pick it up (a gun, or a thrown knife back),
// press G to drop the picked up weapon. Put it on the Player (or the Camera).
// (E is not used because CharacterHealth uses it for the heal test key.)
public class PlayerInteract : MonoBehaviour
{
    [SerializeField] Camera playerCamera;          // if empty, Camera.main
    [SerializeField] WeaponHolder weaponHolder;
    [Tooltip("Optional: thrown knives picked up go back to this weapon.")]
    [SerializeField] ThrowingKnife knifeWeapon;

    [SerializeField] float pickupRange = 2.5f;
    [SerializeField] LayerMask pickupMask = ~0;
    [SerializeField] KeyCode pickupKey = KeyCode.F;
    [SerializeField] KeyCode dropKey = KeyCode.G;

    void Update()
    {
        if (Input.GetKeyDown(dropKey) && weaponHolder != null) weaponHolder.DropPicked();

        if (!Input.GetKeyDown(pickupKey)) return;

        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null) return;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit,
                             pickupRange, pickupMask, QueryTriggerInteraction.Ignore)) return;

        // Weapon lying in the world
        Weapon weapon = hit.collider.GetComponentInParent<Weapon>();
        if (weapon != null && weapon.CanBePickedUp && weaponHolder != null)
        {
            weaponHolder.PickUp(weapon);
            return;
        }

        // Thrown knife stuck somewhere
        ThrownKnife knife = hit.collider.GetComponentInParent<ThrownKnife>();
        if (knife != null && knife.CanCollect && knifeWeapon != null && knifeWeapon.AddKnives(1))
        {
            Destroy(knife.gameObject);
        }
    }
}
