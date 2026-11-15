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

    [Tooltip("Name of the weapon / punch layer in the controller.")]
    [SerializeField] string upperBodyLayer = "Upper Body";

    [Tooltip("How fast the upper body layer fades in and out (weight per second). " +
             "It is only on while that layer plays a clip, so unarmed (or a state without a clip) the arms follow the locomotion.")]
    [SerializeField] float upperBodyBlendSpeed = 8f;

    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    PlayerMovement movement;
    bool warnedMissing;
    Animator layerAnimator;
    Animator configuredAnimator;
    int upperLayerIndex = -1;

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
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                if (!warnedMissing) Debug.LogWarning("PlayerAnimator: no Animator found on the character.");
                warnedMissing = true;
                return false;
            }
            warnedMissing = false;
        }

        // Also when the model swap redirected the field to the new model's Animator (it copies the old settings)
        if (animator != configuredAnimator)
        {
            configuredAnimator = animator;
            animator.applyRootMotion = false;   // root motion would move the model away from the CharacterController (floating, sinking)
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

        UpdateUpperBodyWeight();
    }

    void UpdateUpperBodyWeight()
    {
        if (layerAnimator != animator)
        {
            layerAnimator = animator;
            upperLayerIndex = animator.GetLayerIndex(upperBodyLayer);
        }
        if (upperLayerIndex <= 0) return;

        // During a transition the state it goes to decides (e.g. Empty -> Punch fades in right away)
        int clips = animator.IsInTransition(upperLayerIndex)
            ? animator.GetNextAnimatorClipInfoCount(upperLayerIndex)
            : animator.GetCurrentAnimatorClipInfoCount(upperLayerIndex);

        float target = clips > 0 ? 1f : 0f;
        float weight = Mathf.MoveTowards(animator.GetLayerWeight(upperLayerIndex), target, upperBodyBlendSpeed * Time.deltaTime);
        animator.SetLayerWeight(upperLayerIndex, weight);
    }

    void OnJumped()
    {
        if (animator != null) animator.SetTrigger(JumpHash);
    }
}