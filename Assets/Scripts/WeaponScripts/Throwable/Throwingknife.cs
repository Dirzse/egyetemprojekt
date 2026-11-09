using System.Collections.Generic;
using UnityEngine;

// Dobókés: egy dobás = több kés, enyhe legyezőben. A repülő kés prefabján az Arrow script van.
public class ThrowingKnife : Weapon
{
    [SerializeField] Transform throwPoint;      // üres objektum, Z = dobás iránya
    [SerializeField] Arrow knifePrefab;
    [SerializeField] float throwSpeed = 30f;

    [Header("Többes dobás")]
    [SerializeField] int knivesPerThrow = 3;
    [SerializeField] float spreadAngle = 6f;    // fok két szomszédos kés között (vízszintesen)
    [SerializeField] float sideOffset = 0.12f;  // oldalirányú eltolás, hogy ne ütközzenek egymásba

    public override bool TryGetAmmoType(out AmmoType type) { type = AmmoType.Knife; return true; }

    public override void HandleInput()
    {
        if (!Input.GetMouseButtonDown(0) || !CanUse || !HasAmmo(AmmoType.Knife)) return;

        // Ha kevesebb van hátra, mint 3, azt dobja el, ami maradt
        int count = ConsumeAmmo(AmmoType.Knife, knivesPerThrow);
        if (count <= 0) return;

        nextUseTime = Time.time + cooldown;

        var spawned = new List<Arrow>(count);

        for (int i = 0; i < count; i++)
        {
            float t = i - (count - 1) * 0.5f;   // 3 késnél: -1, 0, +1
            Vector3 dir = Quaternion.AngleAxis(t * spreadAngle, throwPoint.up) * throwPoint.forward;
            Vector3 pos = throwPoint.position + throwPoint.right * (t * sideOffset);

            Arrow knife = Instantiate(knifePrefab, pos, Quaternion.LookRotation(dir));
            knife.Launch(dir * throwSpeed);
            spawned.Add(knife);
        }

        // Az egyszerre dobott kések ne ütközzenek egymásba (különben egymásba akadnak és megállnak a levegőben)
        for (int a = 0; a < spawned.Count; a++)
            for (int b = a + 1; b < spawned.Count; b++)
                foreach (Collider ca in spawned[a].GetComponentsInChildren<Collider>())
                    foreach (Collider cb in spawned[b].GetComponentsInChildren<Collider>())
                        Physics.IgnoreCollision(ca, cb);
    }
}