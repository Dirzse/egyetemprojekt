using UnityEngine;

// Drives the character's Animator from the state exposed by PlayerMovement.
// Put this on the Player object (next to PlayerMovement).
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAnimator : MonoBehaviour
{
    [Tooltip("The Animator on the character model (Survival_Character). Found automatically in children if left empty.")]
    [SerializeField] Animator animator;

    [Tooltip("PlayerAnimator controller. Put on whatever model the player has at runtime (also after the character swap).")]
    [SerializeField] RuntimeAnimatorController controller;

    [Tooltip("Smoothing time for the locomotion blend tree parameters.")]
    [SerializeField] float dampTime = 0.1f;

    [Tooltip("Blend tree value used while running (walk = 1, run = this value).")]
    [SerializeField] float runBlendValue = 2f;

    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    PlayerMovement movement;
    bool warnedMissing;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        EnsureAnimator();
    }

    // Finds the current model's Animator and sets it up. Called every frame, so it also
    // works if the model is swapped (CharacterModelSwap) before or after this script starts.
    bool EnsureAnimator()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                if (!warnedMissing) Debug.LogWarning("PlayerAnimator: no Animator found on the character.");
                warnedMissing = true;
                return false;
            }
            warnedMissing = false;

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.avatar == null)
                Debug.LogWarning("PlayerAnimator: '" + animator.name + "' has no Avatar. Set its FBX Rig to Humanoid.");
        }

        if (controller != null && animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;

        return animator.runtimeAnimatorController != null;
    }

    void OnEnable()  { movement.Jumped += OnJumped; }
    void OnDisable() { movement.Jumped -= OnJumped; }

    void Update()
    {
        if (!EnsureAnimator()) return;

        // Local input direction: x = strafe, y = forward/back. Running scales it up so the
        // 2D blend tree can place run clips at distance 2 and walk clips at distance 1.
        Vector2 input = movement.MoveInput;
        if (input.sqrMagnitude > 1f) input.Normalize();
        float scale = movement.IsRunning ? runBlendValue : 1f;

        animator.SetFloat(MoveXHash, input.x * scale, dampTime, Time.deltaTime);
        animator.SetFloat(MoveYHash, input.y * scale, dampTime, Time.deltaTime);
        animator.SetBool(IsGroundedHash, movement.IsGrounded);
        animator.SetFloat(VerticalVelocityHash, movement.VerticalVelocity);
    }

    void OnJumped()
    {
        if (animator != null) animator.SetTrigger(JumpHash);
    }
}