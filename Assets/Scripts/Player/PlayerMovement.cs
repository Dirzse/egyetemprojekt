using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Komponensek")]
    [HideInInspector] public CharacterController controller;   // taken from this object in Awake

    [Header("Bemenetek (Input Actions)")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference runAction;

    [Header("Mozgás paraméterek")]
    public float walkSpeed = 5f;    // set by PlayerStats from the selected character
    public float runSpeed = 12f;    // set by PlayerStats from the selected character
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Talaj ellenőrzés (Ground Check)")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Editor helpers")]
    [Tooltip("Child object that only shows the facing direction in the editor. Deleted when the game starts.")]
    public string faceMarkerName = "face";

    private Vector3 velocity;
    private bool isGrounded;

    // Read-only movement state for animation (read by PlayerAnimator)
    public bool IsGrounded => isGrounded;
    public float VerticalVelocity => velocity.y;
    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }
    public event System.Action Jumped;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // The capsule mesh is only an editor placeholder, the character model is shown in game
        MeshRenderer placeholder = GetComponent<MeshRenderer>();
        if (placeholder != null) Destroy(placeholder);

        Transform face = FindChildRecursive(transform, faceMarkerName);
        if (face != null) Destroy(face.gameObject);

        if (groundMask.value == 0)
            Debug.LogWarning("PlayerMovement: a groundMask 'Nothing'-ra van állítva, ezért a CheckSphere sosem talál talajt!", this);

        if (groundCheck == null)
            Debug.LogWarning("PlayerMovement: nincs groundCheck megadva, csak a CharacterController.isGrounded lesz használva.", this);
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (string.IsNullOrEmpty(childName)) return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    // An unassigned action (or a reference without an action) is treated as "not pressed" instead of an error
    static bool HasAction(InputActionReference reference) => reference != null && reference.action != null;

    private void OnEnable()
    {
        if (HasAction(moveAction)) moveAction.action.Enable();
        if (HasAction(jumpAction)) jumpAction.action.Enable();
        if (HasAction(runAction)) runAction.action.Enable();
    }

    private void OnDisable()
    {
        if (HasAction(moveAction)) moveAction.action.Disable();
        if (HasAction(jumpAction)) jumpAction.action.Disable();
        if (HasAction(runAction)) runAction.action.Disable();
    }

    void Update()
    {
        // 1. Talaj ellenőrzés (a CharacterController saját ellenőrzése tartalékként)
        bool sphereGrounded = groundCheck != null &&
                              Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, QueryTriggerInteraction.Ignore);
        isGrounded = sphereGrounded || controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        // 2. Vízszintes mozgás
        Vector2 moveInput = HasAction(moveAction) ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        MoveInput = moveInput;
        bool runPressed = HasAction(runAction) && runAction.action.IsPressed();
        IsRunning = runPressed && moveInput.sqrMagnitude > 0.01f;

        // 3. Futás
        float currentSpeed = runPressed ? runSpeed : walkSpeed;

        // 4. Ugrás
        if (HasAction(jumpAction) && jumpAction.action.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Jumped?.Invoke();
        }

        // 5. Gravitáció
        velocity.y += gravity * Time.deltaTime;

        // 6. Egyetlen Move hívás (vízszintes + függőleges mozgás együtt)
        Vector3 finalMove = move * currentSpeed + new Vector3(0f, velocity.y, 0f);
        controller.Move(finalMove * Time.deltaTime);
    }
}