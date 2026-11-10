using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 3D preview of the selected character (ARK-like): the model stands on a pedestal,
// drag with the left mouse button to rotate it, scroll to zoom. Models are spawned once and cached.
// Every model is scaled to the same height, and the camera is placed so the character stands at the
// chosen point of the screen (between the side panels, above the bottom buttons) at any resolution.
public class CharacterPreviewStage : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Spawned models go here; this object is rotated.")]
    [SerializeField] Transform modelRoot;
    [SerializeField] Camera previewCamera;
    [Tooltip("Optional idle animation for the previews (used when the model has an Animator without a controller).")]
    [SerializeField] RuntimeAnimatorController previewAnimator;

    [Header("Framing")]
    [Tooltip("Every model is scaled to this height (meters), so different models look the same size. 0 = keep the model's own size.")]
    [SerializeField] float targetHeight = 1.8f;
    [Tooltip("How much of the screen height the character fills when fully zoomed out (0..1).")]
    [Range(0.2f, 1f)]
    [SerializeField] float screenFill = 0.62f;
    [Tooltip("Where the middle of the character sits on the screen (0..1). x: 0.5 = center, y: higher = the character stands higher.")]
    [SerializeField] Vector2 screenCenter = new Vector2(0.48f, 0.56f);
    [Tooltip("How much the camera looks down at the model.")]
    [SerializeField] float cameraTilt = 0.12f;

    [Header("Rotate")]
    [SerializeField] float dragSpeed = 0.4f;        // degrees per pixel
    [SerializeField] float rotateDamping = 10f;
    [SerializeField] float idleSpinSpeed = 10f;     // degrees per second when untouched, 0 = off
    [SerializeField] float idleSpinDelay = 4f;      // seconds after the last input

    [Header("Zoom")]
    [Tooltip("Largest zoom (1 = whole body, 2.5 = upper body / face).")]
    [SerializeField] float maxZoom = 2.5f;
    [SerializeField] float zoomStep = 0.25f;
    [SerializeField] float zoomDamping = 8f;
    [Tooltip("Height looked at when fully zoomed in, as a part of the character's height (0.85 = chest / face).")]
    [Range(0.5f, 1f)]
    [SerializeField] float zoomFocus = 0.85f;

    [Header("Switch effect")]
    [SerializeField] float popDuration = 0.3f;

    readonly Dictionary<CharacterData, GameObject> cache = new Dictionary<CharacterData, GameObject>();
    readonly Dictionary<GameObject, Vector3> baseScales = new Dictionary<GameObject, Vector3>();

    GameObject currentModel;
    float yaw, targetYaw;
    float zoom = 1f, targetZoom = 1f;
    float lastInputTime;
    bool dragging;
    Vector3 lastMousePosition;
    float popTime = 1f;

    void Awake()
    {
        if (modelRoot == null) modelRoot = transform;
        if (previewCamera == null) previewCamera = Camera.main;
        lastInputTime = Time.time;
        UpdateCamera();
    }

    void Update()
    {
        HandleMouse();

        // Slow turntable spin when nobody touches it
        if (idleSpinSpeed > 0f && !dragging && Time.time - lastInputTime > idleSpinDelay)
            targetYaw += idleSpinSpeed * Time.deltaTime;

        yaw = Mathf.Lerp(yaw, targetYaw, rotateDamping * Time.deltaTime);
        modelRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        zoom = Mathf.Lerp(zoom, targetZoom, zoomDamping * Time.deltaTime);
        UpdateCamera();
        UpdatePop();
    }

    void HandleMouse()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (Input.GetMouseButtonDown(0) && !overUI)
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

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && !overUI)
        {
            targetZoom = Mathf.Clamp(targetZoom + scroll * zoomStep, 1f, Mathf.Max(1f, maxZoom));
            lastInputTime = Time.time;
        }
    }

    void UpdateCamera()
    {
        if (previewCamera == null) return;

        float height = targetHeight > 0f ? targetHeight : 1.8f;
        float halfTan = Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        // Distance where the whole character fills screenFill of the screen, then closer when zoomed
        float distance = height / Mathf.Max(0.05f, screenFill) / (2f * halfTan) / Mathf.Max(1f, zoom);
        float visibleHeight = 2f * distance * halfTan;
        float visibleWidth = visibleHeight * previewCamera.aspect;

        // 0 = fully zoomed out, 1 = fully zoomed in: closer = look higher (chest / face), and centered
        float t = maxZoom > 1f ? Mathf.InverseLerp(1f, maxZoom, zoom) : 0f;
        float focusHeight = Mathf.Lerp(height * 0.5f, height * zoomFocus, t);
        Vector2 offset = Vector2.Lerp(screenCenter - new Vector2(0.5f, 0.5f), Vector2.zero, t);

        // Moving the look point the opposite way moves the character to the wanted screen position
        // (the camera faces the stage from the front, so its right is the stage's left)
        Vector3 lookPoint = transform.position
                          + Vector3.up * (focusHeight - offset.y * visibleHeight)
                          + transform.right * (offset.x * visibleWidth);
        Vector3 direction = (transform.forward + Vector3.up * cameraTilt).normalized;

        previewCamera.transform.position = lookPoint + direction * distance;
        previewCamera.transform.rotation = Quaternion.LookRotation(lookPoint - previewCamera.transform.position);
    }

    // Shows the model of the given character (spawns it the first time)
    public void Show(CharacterData data)
    {
        if (currentModel) currentModel.SetActive(false);
        currentModel = null;
        if (data == null || data.previewModel == null) return;

        if (!cache.TryGetValue(data, out GameObject model) || model == null)
        {
            model = Instantiate(data.previewModel, modelRoot);
            model.name = "Preview_" + data.characterName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;   // faces the camera (+Z)
            PreparePreview(model);
            FitHeight(model);
            cache[data] = model;
            baseScales[model] = model.transform.localScale;
        }

        model.SetActive(true);
        currentModel = model;

        // Turn back to the front and play a small "pop" scale
        targetYaw = Mathf.Round(yaw / 360f) * 360f;
        lastInputTime = Time.time;
        popTime = 0f;
        UpdatePop();
    }

    // The preview is only a model: turn off any gameplay scripts, colliders and physics on it
    void PreparePreview(GameObject model)
    {
        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider col in model.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;

        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController == null && previewAnimator != null)
                animator.runtimeAnimatorController = previewAnimator;
        }
    }

    // Scales the model to targetHeight and puts its feet on the pedestal
    void FitHeight(GameObject model)
    {
        if (targetHeight <= 0f) return;
        if (!TryGetBounds(model, out Bounds bounds) || bounds.size.y < 0.001f) return;

        model.transform.localScale *= targetHeight / bounds.size.y;

        TryGetBounds(model, out bounds);
        model.transform.position += Vector3.up * (modelRoot.position.y - bounds.min.y);
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

    void UpdatePop()
    {
        if (currentModel == null || popTime >= 1f) return;
        popTime = Mathf.Min(1f, popTime + Time.deltaTime / Mathf.Max(0.01f, popDuration));
        currentModel.transform.localScale = baseScales[currentModel] * EaseOutBack(popTime);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}