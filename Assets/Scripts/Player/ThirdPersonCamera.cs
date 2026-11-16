using UnityEngine;
using UnityEngine.InputSystem;

// Third person camera over the right shoulder. The Switch View action (Input System) switches between first and third person.
// Put it on the player's Camera next to MouseLook: MouseLook still turns the body and tilts the camera,
// this script only moves the camera back behind the player and keeps it out of walls.
// While the right mouse button is held with a weapon in hand, the camera comes closer to the shoulder for aiming.
[DefaultExecutionOrder(100)]   // after MouseLook
public class ThirdPersonCamera : MonoBehaviour
{
    [Tooltip("Off = the game starts in first person.")]
    [SerializeField] bool startInThirdPerson = false;
    [Tooltip("Button action that switches between first and third person (e.g. Player/SwitchView).")]
    [SerializeField] InputActionReference switchViewAction;
    [Tooltip("Optional: aim button action (camera comes closer while held). Empty = right mouse button.")]
    [SerializeField] InputActionReference aimAction;

    [Header("Third person")]
    [Tooltip("How far behind the head the camera is.")]
    [SerializeField] float distance = 3f;
    [Tooltip("Sideways offset (positive = over the right shoulder).")]
    [SerializeField] float shoulderOffset = 0.6f;
    [Tooltip("Extra height above the head.")]
    [SerializeField] float heightOffset = 0.15f;

    [Header("Aiming (right mouse button with a weapon)")]
    [SerializeField] float aimDistance = 1.6f;
    [SerializeField] float aimShoulderOffset = 0.7f;

    [Header("Walls")]
    [Tooltip("The camera stays this far from walls.")]
    [SerializeField] float collisionRadius = 0.25f;
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField] float smoothing = 12f;

    Transform player;
    WeaponHolder weaponHolder;
    Vector3 firstPersonLocal;
    float currentDistance;
    float currentShoulder;
    float blend;   // 0 = first person, 1 = third person
    bool thirdPerson;

    readonly RaycastHit[] hits = new RaycastHit[16];

    public bool IsThirdPerson => thirdPerson;

    // The point the camera orbits around (the eyes); melee hits and pickups start from here in third person
    public Vector3 PivotWorld => transform.parent != null ? transform.parent.TransformPoint(PivotLocal) : transform.position;
    Vector3 PivotLocal => new Vector3(firstPersonLocal.x, firstPersonLocal.y, 0f);

    void Start()
    {
        firstPersonLocal = transform.localPosition;   // the first person (eye) position set in the editor
        PlayerMovement movement = GetComponentInParent<PlayerMovement>();
        player = movement != null ? movement.transform : transform.parent;
        thirdPerson = startInThirdPerson;
        blend = thirdPerson ? 1f : 0f;
        currentDistance = distance;
        currentShoulder = shoulderOffset;
    }

    static bool HasAction(InputActionReference reference) => reference != null && reference.action != null;

    void OnEnable()
    {
        if (HasAction(switchViewAction)) switchViewAction.action.Enable();
        if (HasAction(aimAction)) aimAction.action.Enable();
    }

    void OnDisable()
    {
        if (HasAction(switchViewAction)) switchViewAction.action.Disable();
        if (HasAction(aimAction)) aimAction.action.Disable();
    }

    void Update()
    {
        if (HasAction(switchViewAction) && switchViewAction.action.WasPressedThisFrame() && Time.timeScale > 0f)
            thirdPerson = !thirdPerson;
    }

    bool AimHeld => HasAction(aimAction) ? aimAction.action.IsPressed() : Mouse.current != null && Mouse.current.rightButton.isPressed;

    void LateUpdate()
    {
        if (transform.parent == null) return;
        if (weaponHolder == null && player != null) weaponHolder = player.GetComponentInChildren<WeaponHolder>();

        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        blend = Mathf.Lerp(blend, thirdPerson ? 1f : 0f, k);

        bool aiming = AimHeld && weaponHolder != null && weaponHolder.Current != null;
        currentDistance = Mathf.Lerp(currentDistance, aiming ? aimDistance : distance, k);
        currentShoulder = Mathf.Lerp(currentShoulder, aiming ? aimShoulderOffset : shoulderOffset, k);

        // Behind the pivot along the camera's own tilt (MouseLook sets localRotation every frame)
        Transform parent = transform.parent;
        Vector3 pivotLocal = PivotLocal;
        Vector3 offsetLocal = transform.localRotation * new Vector3(currentShoulder, heightOffset, -currentDistance);
        Vector3 pivotWorld = parent.TransformPoint(pivotLocal);
        Vector3 wantedWorld = parent.TransformPoint(pivotLocal + offsetLocal);

        // Do not go through walls: stop in front of the first thing between the head and the camera
        Vector3 toCamera = wantedWorld - pivotWorld;
        float length = toCamera.magnitude;
        if (length > 0.001f)
        {
            float free = length;
            int count = Physics.SphereCastNonAlloc(pivotWorld, collisionRadius, toCamera / length, hits, length, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (player != null && hits[i].transform.IsChildOf(player)) continue;   // own body and weapons
                if (hits[i].distance < free) free = hits[i].distance;
            }
            wantedWorld = pivotWorld + toCamera / length * free;
        }

        Vector3 firstPersonWorld = parent.TransformPoint(firstPersonLocal);
        transform.position = Vector3.Lerp(firstPersonWorld, wantedWorld, blend);
    }
}
