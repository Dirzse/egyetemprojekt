using UnityEngine;

// Zoom (FOV) + célkereszt kezelése az aktuális fegyver alapján.
public class AimController : MonoBehaviour
{
    [SerializeField] WeaponHolder holder;
    [SerializeField] Camera cam;
    [SerializeField] GameObject crosshair;     // pl. egy kis UI Image a Canvas közepén
    [SerializeField] float zoomSpeed = 12f;

    float normalFov;

    void Start() => normalFov = cam.fieldOfView;

    void Update()
    {
        Weapon w = holder.Current;
        bool aiming = w != null && w.IsAiming;

        float target = aiming ? w.AimFov : normalFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));

        if (crosshair) crosshair.SetActive(w != null && w.ShowCrosshair);
    }
}