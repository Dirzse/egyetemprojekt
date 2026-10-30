using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    [SerializeField] Weapon[] weapons;   // 0 = íj, 1 = kard
    int current = -1;

    void Start()
    {
        foreach (var w in weapons) w.OnUnequip();
        Equip(0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(1);

        if (current >= 0) weapons[current].HandleInput();
    }

    void Equip(int index)
    {
        if (index < 0 || index >= weapons.Length || index == current) return;
        if (current >= 0) weapons[current].OnUnequip();
        current = index;
        weapons[current].OnEquip();
    }
}
