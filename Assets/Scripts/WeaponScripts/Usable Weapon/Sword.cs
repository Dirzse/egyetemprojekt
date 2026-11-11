using System.Collections;
using UnityEngine;

public class Sword : Weapon
{
    [SerializeField] Camera playerCamera;
    [SerializeField] Animator animator;          // opcionális: "Swing" trigger, ha van saját animációd

    [Header("Ütés")]
    [SerializeField] float range = 2.2f;
    [SerializeField] float radius = 0.5f;
    [SerializeField] float damage = 30f;
    [SerializeField] LayerMask hitMask = ~0;

    [Header("Lengés (kódból, animáció nélkül)")]
    [SerializeField] Transform swingTarget;      // amit mozgatunk (üresen: maga a kard objektum)
    [SerializeField] Vector3 windupEuler  = new Vector3(-15f, 20f, 35f);    // ráfeszülés: forgatás
    [SerializeField] Vector3 windupOffset = new Vector3(0.05f, 0.05f, -0.05f);
    [SerializeField] Vector3 slashEuler   = new Vector3(35f, -45f, -50f);   // csapás: forgatás
    [SerializeField] Vector3 slashOffset  = new Vector3(-0.15f, -0.1f, 0.15f);
    [SerializeField] float windupTime  = 0.12f;
    [SerializeField] float slashTime   = 0.10f;
    [SerializeField] float recoverTime = 0.25f;

    [Header("Effekt")]
    [SerializeField] GameObject hitEffect;       // particle prefab a találat helyén
    [SerializeField] AudioClip hitSound;

    Vector3 restPos;
    Quaternion restRot;
    bool restCaptured;

    void Awake()
    {
        if (!swingTarget) swingTarget = transform;
        restPos = swingTarget.localPosition;
        restRot = swingTarget.localRotation;
        restCaptured = true;
    }

    public override void OnEquip()
    {
        base.OnEquip();
        ApplyRest();
    }

    public override void OnUnequip()
    {
        StopAllCoroutines();
        ApplyRest();
        base.OnUnequip();
    }

    void ApplyRest()
    {
        if (!restCaptured) return;
        swingTarget.localPosition = restPos;
        swingTarget.localRotation = restRot;
    }

    public override void HandleInput()
    {
        if (!Input.GetMouseButtonDown(0) || !CanUse) return;

        nextUseTime = Time.time + Mathf.Max(cooldown, windupTime + slashTime + recoverTime);
        if (animator) animator.SetTrigger("Swing");
        StartCoroutine(Swing());
    }

    IEnumerator Swing()
    {
        Vector3 windPos = restPos + windupOffset;
        Quaternion windRot = restRot * Quaternion.Euler(windupEuler);
        Vector3 slashPos = restPos + slashOffset;
        Quaternion slashRot = restRot * Quaternion.Euler(slashEuler);

        yield return Blend(restPos, restRot, windPos, windRot, windupTime);
        DoHit();   // a csapás kezdetén sebez
        yield return Blend(windPos, windRot, slashPos, slashRot, slashTime);
        yield return Blend(slashPos, slashRot, restPos, restRot, recoverTime);
    }

    IEnumerator Blend(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float time)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(time, 0.001f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            swingTarget.localPosition = Vector3.Lerp(fromPos, toPos, k);
            swingTarget.localRotation = Quaternion.Slerp(fromRot, toRot, k);
            yield return null;
        }
    }

    void DoHit()
    {
        Transform cam = playerCamera.transform;
        // Skips the player's own body and weapons (the sword model has colliders too)
        if (DamageUtil.SphereCastIgnoring(cam.position, radius, cam.forward, range, hitMask, transform.root, out RaycastHit hit))
        {
            DamageUtil.Apply(hit.collider, damage * DamageMultiplier);

            if (hitEffect)
            {
                GameObject fx = Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, 3f);
            }
            if (hitSound) AudioSource.PlayClipAtPoint(hitSound, hit.point);
        }
    }
}