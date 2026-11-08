using UnityEngine;

// Dobókés: a repülő kés prefabján az Arrow script van (beleragad, a haladási irányba fordul).
public class ThrowingKnife : Weapon
{
    [SerializeField] Transform throwPoint;     // üres objektum a kézben, Z = dobás iránya
    [SerializeField] Arrow knifePrefab;
    [SerializeField] float throwSpeed = 30f;
    [SerializeField] int ammo = 5;

    public override void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanUse && ammo > 0)
        {
            ammo--;
            nextUseTime = Time.time + cooldown;

            Arrow knife = Instantiate(knifePrefab, throwPoint.position, Quaternion.LookRotation(throwPoint.forward));
            knife.Launch(throwPoint.forward * throwSpeed);
        }
    }
}