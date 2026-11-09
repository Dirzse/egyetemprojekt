using System;
using System.Collections.Generic;
using UnityEngine;

public enum AmmoType { Arrow, Knife, Grenade, Bullet }

// A playerre kerül. Minden lőszer itt van, a fegyverek innen vonnak le, az ammo box ide ad hozzá.
public class AmmoInventory : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public AmmoType type;
        public int amount;
        public int max = 99;
    }

    [SerializeField] List<Slot> slots = new List<Slot>
    {
        new Slot { type = AmmoType.Arrow,   amount = 20, max = 60 },
        new Slot { type = AmmoType.Knife,   amount = 9,  max = 30 },
        new Slot { type = AmmoType.Grenade, amount = 3,  max = 10 },
        new Slot { type = AmmoType.Bullet,  amount = 30, max = 150 },
    };

    // A HUD később erre iratkozik fel (típus, új mennyiség)
    public event Action<AmmoType, int> Changed;

    Slot Find(AmmoType type)
    {
        foreach (var s in slots) if (s.type == type) return s;
        return null;
    }

    public int Get(AmmoType type) => Find(type)?.amount ?? 0;

    public bool Has(AmmoType type, int n = 1) => Get(type) >= n;

    // Levon legfeljebb n darabot, visszaadja, mennyit sikerült ténylegesen
    public int Consume(AmmoType type, int n = 1)
    {
        Slot s = Find(type);
        if (s == null || n <= 0) return 0;
        int taken = Mathf.Min(n, s.amount);
        if (taken > 0)
        {
            s.amount -= taken;
            Changed?.Invoke(type, s.amount);
        }
        return taken;
    }

    // Hozzáad, a max-ig. Visszaadja, mennyi fért bele.
    public int Add(AmmoType type, int n)
    {
        Slot s = Find(type);
        if (s == null || n <= 0) return 0;
        int added = Mathf.Min(n, s.max - s.amount);
        if (added > 0)
        {
            s.amount += added;
            Changed?.Invoke(type, s.amount);
        }
        return added;
    }

    // Tesztelés: Inspectorban a komponens jobb klikk menüjéből
    [ContextMenu("Refill all (test)")]
    void RefillAll()
    {
        foreach (var s in slots) { s.amount = s.max; Changed?.Invoke(s.type, s.amount); }
    }
}