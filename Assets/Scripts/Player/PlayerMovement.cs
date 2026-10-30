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
    public float speed = 6f; 
    private float gravity = -9.81f; 
    public float jumpHeight = 1.5f; 

    [Header("Talaj ellenőrzés (Ground Check)")]
    public Transform groundCheck; 
    public float groundDistance = 0.4f; 
    public LayerMask groundMask; 

    private Vector3 velocity;
    private bool isGrounded;

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
        // 1. Talaj ellenőrzés
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 2. Vízszintes mozgás
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        float x = moveInput.x;
        float z = moveInput.y;

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        if (jumpAction.action.triggered && isGrounded) 
        {
            
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (runAction.action.IsPressed())
        {
            speed = 12f; 
        }
        else
        {
            speed = 5f; 
        }
        
        // 4. Gravitáció alkalmazása
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}