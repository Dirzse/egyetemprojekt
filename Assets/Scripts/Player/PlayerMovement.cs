using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Komponensek")]
    public CharacterController controller;

    [Header("Bemenetek (Input Actions)")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference runAction;

    [Header("Mozgás paraméterek")]
    public float walkSpeed = 5f;
    public float runSpeed = 12f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Talaj ellenőrzés (Ground Check)")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private Vector3 velocity;
    private bool isGrounded;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (groundMask.value == 0)
            Debug.LogWarning("PlayerMovement: a groundMask 'Nothing'-ra van állítva, ezért a CheckSphere sosem talál talajt!", this);

        if (groundCheck == null)
            Debug.LogWarning("PlayerMovement: nincs groundCheck megadva, csak a CharacterController.isGrounded lesz használva.", this);
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
        if (runAction != null) runAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
        if (runAction != null) runAction.action.Disable();
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
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        // 3. Futás
        float currentSpeed = runAction.action.IsPressed() ? runSpeed : walkSpeed;

        // 4. Ugrás
        if (jumpAction.action.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 5. Gravitáció
        velocity.y += gravity * Time.deltaTime;

        // 6. Egyetlen Move hívás (vízszintes + függőleges mozgás együtt)
        Vector3 finalMove = move * currentSpeed + new Vector3(0f, velocity.y, 0f);
        controller.Move(finalMove * Time.deltaTime);
    }
}