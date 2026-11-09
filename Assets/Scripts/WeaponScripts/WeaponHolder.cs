using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    [SerializeField] Weapon[] weapons;   // 1. íj, 2. kard, 3. dobókés, 4. gránát ...
    int current = -1;

    public Weapon Current => current >= 0 ? weapons[current] : null;

    void Start()
    {
        foreach (var w in weapons) w.OnUnequip();
        Equip(0);
    }

    void Update()
    {
        // 1-9 gombok: fegyverváltás
        for (int i = 0; i < weapons.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Equip(i);

        if (Current == null) return;

        Current.SetAiming(Input.GetMouseButton(1));   // jobb klikk = célzás
        Current.HandleInput();
    }

    void Equip(int index)
    {
        if (index < 0 || index >= weapons.Length || index == current) return;
        if (current >= 0) weapons[current].OnUnequip();
        current = index;
        weapons[current].OnEquip();
    }
}