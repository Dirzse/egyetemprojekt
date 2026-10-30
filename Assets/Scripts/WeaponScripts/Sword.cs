using UnityEngine;

public class Sword : Weapon
{
    [SerializeField] Camera playerCamera;
    [SerializeField] Animator animator;         // opcionális, "Swing" trigger
    [SerializeField] float range = 2.2f;
    [SerializeField] float radius = 0.5f;
    [SerializeField] float damage = 30f;
    [SerializeField] float hitDelay = 0.15f;    // az ütés "becsapódási" pillanata az animáción belül
    [SerializeField] LayerMask hitMask = ~0;

    public override void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanUse)
        {
            nextUseTime = Time.time + cooldown;
            if (animator) animator.SetTrigger("Swing");
            Invoke(nameof(DoHit), hitDelay);
        }
    }

    void DoHit()
    {
        Transform cam = playerCamera.transform;
        // SphereCast a kamerából előre: belső nézetben így érzi jónak az ütést
        if (Physics.SphereCast(cam.position, radius, cam.forward, out RaycastHit hit,
                               range, hitMask, QueryTriggerInteraction.Ignore))
        {
            hit.collider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
    }

    public override void OnUnequip()
    {
        CancelInvoke();
        base.OnUnequip();
    }
}
