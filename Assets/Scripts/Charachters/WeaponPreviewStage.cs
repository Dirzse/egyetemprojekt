using System.Collections.Generic;
using UnityEngine;

// Small 3D preview of the selected starting weapon. A second camera draws only into the screen area of a
// UI frame (viewArea), so no RenderTexture is needed. Drag inside the frame to rotate the weapon.
// Any model is auto-scaled and centered, so new weapons (e.g. the gun) just need their prefab assigned.
public class WeaponPreviewStage : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera weaponCamera;
    [Tooltip("The UI frame the weapon camera draws into (screen space overlay canvas).")]
    [SerializeField] RectTransform viewArea;
    [Tooltip("Spawned models go here; this object is rotated.")]
    [SerializeField] Transform modelRoot;
    [Tooltip("Shown when the selected weapon has no preview model yet.")]
    [SerializeField] GameObject noModelLabel;

    [Header("Model")]
    [Tooltip("Longest side of the model after auto-fit (meters).")]
    [SerializeField] float targetSize = 1f;
    [Tooltip("Lay tall models (e.g. a sword standing up) on their side, so they fit the wide frame.")]
    [SerializeField] bool layTallModelsDown = true;
    [SerializeField] float tilt = 15f;            // degrees towards the camera

    [Header("Rotate")]
    [SerializeField] float dragSpeed = 0.5f;      // degrees per pixel
    [SerializeField] float rotateDamping = 10f;
    [SerializeField] float idleSpinSpeed = 30f;   // degrees per second when untouched
    [SerializeField] float idleSpinDelay = 2f;

    readonly Dictionary<GameObject, GameObject> cache = new Dictionary<GameObject, GameObject>();
    readonly Vector3[] corners = new Vector3[4];
    GameObject current;
    float yaw, targetYaw;
    bool dragging;
    Vector3 lastMousePosition;
    float lastInputTime = -100f;

    void LateUpdate()
    {
        UpdateViewport();
        HandleMouse();

        if (idleSpinSpeed > 0f && !dragging && Time.time - lastInputTime > idleSpinDelay)
            targetYaw += idleSpinSpeed * Time.deltaTime;

        yaw = Mathf.Lerp(yaw, targetYaw, rotateDamping * Time.deltaTime);
        // Spin around the world up axis, then tilt towards the camera (the tilt does not spin with it)
        modelRoot.rotation = Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.AngleAxis(yaw, Vector3.up);
    }

    // Puts the weapon camera's viewport exactly on the UI frame (works at any resolution)
    void UpdateViewport()
    {
        if (weaponCamera == null || viewArea == null) return;
        viewArea.GetWorldCorners(corners);   // overlay canvas: these are screen pixels
        float x = corners[0].x / Screen.width;
        float y = corners[0].y / Screen.height;
        float w = (corners[2].x - corners[0].x) / Screen.width;
        float h = (corners[2].y - corners[0].y) / Screen.height;
        weaponCamera.rect = new Rect(x, y, w, h);
    }

    void HandleMouse()
    {
        if (viewArea == null) return;

        if (Input.GetMouseButtonDown(0) &&
            RectTransformUtility.RectangleContainsScreenPoint(viewArea, Input.mousePosition, null))
        {
            dragging = true;
            lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0)) dragging = false;

        if (dragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            lastMousePosition = Input.mousePosition;
            targetYaw -= delta.x * dragSpeed;
            lastInputTime = Time.time;
        }
    }

    public void Show(GameObject prefab)
    {
        if (current) current.SetActive(false);
        current = null;
        if (noModelLabel) noModelLabel.SetActive(prefab == null);
        if (prefab == null) return;

        if (!cache.TryGetValue(prefab, out GameObject model) || model == null)
        {
            model = Instantiate(prefab, modelRoot);
            model.name = "Preview_" + prefab.name;
            PreparePreview(model);
            Fit(model);
            cache[prefab] = model;
        }

        model.SetActive(true);
        current = model;
        lastInputTime = Time.time - idleSpinDelay;   // start spinning right away
    }

    // The preview is only a model: no scripts, colliders or physics
    static void PreparePreview(GameObject model)
    {
        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider col in model.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
    }

    // Scales the model so its longest side is targetSize, and centers it on the model root
    void Fit(GameObject model)
    {
        Quaternion savedRotation = modelRoot.rotation;
        modelRoot.rotation = Quaternion.identity;

        Transform t = model.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        Bounds bounds;
        if (!TryGetBounds(model, out bounds)) { modelRoot.rotation = savedRotation; return; }

        if (layTallModelsDown && bounds.size.y > bounds.size.x && bounds.size.y > bounds.size.z)
        {
            t.localRotation = Quaternion.Euler(0f, 0f, 90f);
            TryGetBounds(model, out bounds);
        }

        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest > 0.0001f) t.localScale *= targetSize / longest;

        TryGetBounds(model, out bounds);
        t.position += modelRoot.position - bounds.center;

        modelRoot.rotation = savedRotation;
    }

    static bool TryGetBounds(GameObject model, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
