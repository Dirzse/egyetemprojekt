using UnityEngine;

// Zoom (FOV) + célkereszt kezelése az aktuális fegyver alapján.
public class AimController : MonoBehaviour
{
    [SerializeField] WeaponHolder holder;      // if empty, found in the scene
    [SerializeField] Camera cam;               // if empty, Camera.main
    [SerializeField] GameObject crosshair;     // pl. egy kis UI Image a Canvas közepén
    [SerializeField] float zoomSpeed = 12f;

    float normalFov;

    void Start()
    {
        if (!holder) holder = FindFirstObjectByType<WeaponHolder>();
        if (!cam) cam = Camera.main;
        if (cam) normalFov = cam.fieldOfView;

        if (!holder) Debug.LogWarning("AimController: no WeaponHolder found.", this);
        if (!cam) Debug.LogWarning("AimController: no camera found.", this);
    }

    void Update()
    {
        if (!holder || !cam) return;

        Weapon w = holder.Current;
        bool aiming = w != null && w.IsAiming;

        float target = aiming ? w.AimFov : normalFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));

        if (crosshair) crosshair.SetActive(w != null && w.ShowCrosshair);
    }
}